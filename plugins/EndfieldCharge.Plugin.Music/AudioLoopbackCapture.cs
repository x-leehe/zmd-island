using System;
using System.Runtime.InteropServices;
using System.Threading;
using EndfieldCharge.Services;

namespace EndfieldCharge.Host.Plugins.Music;

/// <summary>
/// WASAPI loopback 采集：抓「默认播放设备正在放什么」，转成单声道 float 样本供频谱分析。
/// <para>
/// 自写 COM 互操作、**不引第三方包**（照 <c>Services/PowerNative.cs</c> 的路子）；
/// 混音格式按 cava 的 <c>mono_option = average</c> 取多声道平均。
/// </para>
/// <para>
/// **调用方必须先过白名单闸门**：本对象没有"采样识别"能力 —— 它抓到的是整台设备的混音，
/// 所以"只采集允许的来源"只能靠**不创建它**来保证（见 <see cref="SpectrumWhitelist"/>）。
/// </para>
/// </summary>
public sealed class AudioLoopbackCapture : IDisposable
{
    private const int ShareModeShared = 0;
    private const int LoopbackFlag = 0x00020000;
    private const int SilentBufferFlag = 0x2;
    private const int BufferDurationTicks = 10_000_000;   // 100ns 单位 → 1 秒
    private const int PollIntervalMs = 5;

    private readonly object _gate = new();
    private readonly float[] _snapshot;
    private readonly float[] _mono;

    private Thread? _thread;
    private volatile bool _running;
    private bool _hasData;
    private bool _packetFailureLogged;

    private IAudioClient? _client;
    private IAudioCaptureClient? _capture;

    private int _sampleRate = 48000;
    private int _channels = 2;
    private int _bytesPerSample = 4;
    private bool _isFloat = true;

    /// <param name="snapshotSize">对外提供的最新样本数（与频谱分析的 FFT 长度一致）。</param>
    public AudioLoopbackCapture(int snapshotSize = 1024)
    {
        if (snapshotSize < 64)
            throw new ArgumentOutOfRangeException(nameof(snapshotSize), snapshotSize, "快照太短");

        _snapshot = new float[snapshotSize];
        _mono = new float[Math.Max(snapshotSize, 8192)];
    }

    /// <summary>是否正在采集。</summary>
    public bool IsRunning => _running;

    /// <summary>设备混音采样率（Hz）。</summary>
    public int SampleRate => _sampleRate;

    /// <summary>
    /// 启动采集（幂等）。任何失败都返回 <c>false</c> 并清理干净 ——
    /// 设备被独占 / 没有默认输出设备 / 接口不可用都会走到这里，属正常情况。
    /// </summary>
    public bool Start()
    {
        if (_running)
            return true;

        try
        {
            var enumerator = (IMMDeviceEnumerator)Activator.CreateInstance(
                Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"))!)!;

            // eRender(0) + eConsole(0)：默认播放设备
            if (enumerator.GetDefaultAudioEndpoint(0, 0, out var device) != 0 || device is null)
            {
                Logger.Warn("Music: 频谱采集启动失败 —— 拿不到默认播放设备");
                return false;
            }

            var clientId = typeof(IAudioClient).GUID;
            if (device.Activate(ref clientId, 1 /*CLSCTX_INPROC_SERVER*/, IntPtr.Zero, out var clientObject) != 0 ||
                clientObject is not IAudioClient client)
            {
                Logger.Warn("Music: 频谱采集启动失败 —— 无法激活 IAudioClient");
                return false;
            }

            if (client.GetMixFormat(out var formatPtr) != 0 || formatPtr == IntPtr.Zero)
            {
                Logger.Warn("Music: 频谱采集启动失败 —— 读不到混音格式");
                Release(client);
                return false;
            }

            // 混音格式指针只在 Initialize 期间有效 —— 必须先读完 + 初始化完再释放（别提前 Free）
            int initResult;
            try
            {
                ReadFormat(formatPtr);
                initResult = client.Initialize(ShareModeShared, LoopbackFlag, BufferDurationTicks, 0, formatPtr, IntPtr.Zero);
            }
            finally
            {
                Marshal.FreeCoTaskMem(formatPtr);
            }

            if (initResult != 0)
            {
                // 常见原因：设备忙 / 独占模式 / 采样率不匹配
                Logger.Warn($"Music: 频谱采集启动失败 —— Initialize 被拒（{_sampleRate}Hz {_channels}ch {_bytesPerSample * 8}bit）");
                Release(client);
                return false;
            }

            var captureId = typeof(IAudioCaptureClient).GUID;
            if (client.GetService(ref captureId, out var captureObject) != 0 ||
                captureObject is not IAudioCaptureClient capture)
            {
                Logger.Warn("Music: 频谱采集启动失败 —— 拿不到 IAudioCaptureClient");
                Release(client);
                return false;
            }

            // IAudioClient 光 Initialize 还不出数据：**必须再 Start()**。
            // 漏掉这一句时 GetNextPacketSize 永远是"0 个包"，表现成"采集已启动、但一个样本都拿不到"——
            // 日志上完全看不出问题（这正是"展开态背景频谱没跑起来"的根因）。
            if (client.Start() != 0)
            {
                Logger.Warn("Music: 频谱采集启动失败 —— IAudioClient.Start 被拒");
                Release(capture);
                Release(client);
                return false;
            }

            _client = client;
            _capture = capture;
            _running = true;

            _thread = new Thread(CaptureLoop)
            {
                IsBackground = true,
                Name = "music-spectrum-capture",
            };
            _thread.Start();

            Logger.Info($"Music: 频谱采集已启动（loopback {_sampleRate}Hz {_channels}ch，{(_isFloat ? "float" : "pcm")}{_bytesPerSample * 8}）");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Warn($"Music: 频谱采集启动异常 —— {ex.GetType().Name} {ex.Message}");
            Stop();
            return false;
        }
    }

    /// <summary>停止采集并释放设备（幂等）。</summary>
    public void Stop()
    {
        _running = false;

        var thread = _thread;
        _thread = null;
        if (thread is not null && thread.IsAlive && !ReferenceEquals(thread, Thread.CurrentThread))
        {
            try
            {
                thread.Join(500);
            }
            catch (Exception)
            {
                // 线程收尾失败不影响释放
            }
        }

        var client = _client;
        var capture = _capture;
        _client = null;
        _capture = null;

        if (capture is not null)
            Release(capture);

        if (client is not null)
        {
            try
            {
                client.Stop();
            }
            catch (Exception)
            {
                // 已经停了
            }

            Release(client);
        }
    }

    /// <summary>
    /// 复制最近 <c>destination.Length</c> 个单声道样本（不足则保持旧数据并返回 <c>false</c>）。
    /// </summary>
    public bool TryReadLatest(Span<float> destination)
    {
        if (destination.Length == 0)
            return false;

        lock (_gate)
        {
            if (!_hasData)
                return false;

            int count = Math.Min(destination.Length, _snapshot.Length);
            int offset = _snapshot.Length - count;
            for (int i = 0; i < count; i++)
                destination[i] = _snapshot[offset + i];

            return true;
        }
    }

    /// <inheritdoc />
    public void Dispose() => Stop();

    // ---------------- 采集线程 ----------------

    private void CaptureLoop()
    {
        while (_running)
        {
            try
            {
                var capture = _capture;
                if (capture is null)
                    break;

                int hr = capture.GetNextPacketSize(out int pending);
                if (hr != 0)
                {
                    // 只记第一次：这行是"采集启动了但拿不到样本"的第一现场
                    if (!_packetFailureLogged)
                    {
                        _packetFailureLogged = true;
                        Logger.Warn($"Music: 频谱采集取包失败（HRESULT 0x{hr:X8}）");
                    }

                    Thread.Sleep(PollIntervalMs);
                }
                else if (pending == 0)
                {
                    Thread.Sleep(PollIntervalMs);
                }
                else
                {
                    Drain(capture);
                }
            }
            catch (Exception ex)
            {
                // 设备被拔掉 / 被独占 / 会话失效：退出线程，由插件的 tick 决定是否重建
                Logger.Warn($"Music: 频谱采集循环结束 —— {ex.GetType().Name} {ex.Message}");
                break;
            }
        }

        _running = false;
    }

    private void Drain(IAudioCaptureClient capture)
    {
        while (_running && capture.GetNextPacketSize(out int pending) == 0 && pending > 0)
        {
            if (capture.GetBuffer(out var data, out int frames, out int flags, out _, out _) != 0)
                break;

            try
            {
                if (frames > 0)
                    Append(data, frames, (flags & SilentBufferFlag) != 0);
            }
            finally
            {
                capture.ReleaseBuffer(frames);
            }
        }
    }

    /// <summary>把一段设备混音转成单声道 float，并滚动进快照。</summary>
    private void Append(IntPtr data, int frames, bool silent)
    {
        if (frames > _mono.Length)
            frames = _mono.Length;

        lock (_gate)
        {
            if (silent)
            {
                for (int i = 0; i < frames; i++)
                    _mono[i] = 0f;
            }
            else
            {
                ReadMono(data, frames);
            }

            int count = Math.Min(frames, _snapshot.Length);
            if (count >= _snapshot.Length)
            {
                Array.Copy(_mono, frames - count, _snapshot, 0, count);
            }
            else
            {
                Array.Copy(_snapshot, count, _snapshot, 0, _snapshot.Length - count);
                Array.Copy(_mono, 0, _snapshot, _snapshot.Length - count, count);
            }

            _hasData = true;
        }
    }

    /// <summary>按混音格式读样本并做多声道平均（对齐 cava 的 <c>mono_option = average</c>）。</summary>
    private void ReadMono(IntPtr data, int frames)
    {
        int channels = Math.Max(1, _channels);
        int total = frames * channels;
        float scale = _isFloat ? 1f : (1f / (1 << (_bytesPerSample * 8 - 1)));

        if (_isFloat)
        {
            var raw = new float[total];
            Marshal.Copy(data, raw, 0, total);
            for (int i = 0; i < frames; i++)
            {
                float sum = 0f;
                for (int c = 0; c < channels; c++)
                    sum += raw[i * channels + c];

                _mono[i] = sum / channels;
            }

            return;
        }

        if (_bytesPerSample == 2)
        {
            var raw = new short[total];
            Marshal.Copy(data, raw, 0, total);
            for (int i = 0; i < frames; i++)
            {
                float sum = 0f;
                for (int c = 0; c < channels; c++)
                    sum += raw[i * channels + c] * scale;

                _mono[i] = sum / channels;
            }

            return;
        }

        var raw32 = new int[total];
        Marshal.Copy(data, raw32, 0, total);
        for (int i = 0; i < frames; i++)
        {
            float sum = 0f;
            for (int c = 0; c < channels; c++)
                sum += raw32[i * channels + c] * scale;

            _mono[i] = sum / channels;
        }
    }

    // ---------------- 格式与释放 ----------------

    private void ReadFormat(IntPtr formatPtr)
    {
        int tag = Marshal.ReadInt16(formatPtr, 0);
        _channels = Math.Max(1, (int)Marshal.ReadInt16(formatPtr, 2));
        _sampleRate = Math.Max(8000, Marshal.ReadInt32(formatPtr, 4));
        _bytesPerSample = Math.Max(1, (int)Marshal.ReadInt16(formatPtr, 14) / 8);

        // 3 = IEEE_FLOAT；0xFFFE = EXTENSIBLE（绝大多数设备是 32bit float，按 float 处理）
        _isFloat = tag == 3 || (tag == unchecked((short)0xFFFE) && _bytesPerSample == 4);

        if (_bytesPerSample == 1)
            _bytesPerSample = 2;    // 8bit 极少见，按 16bit 处理避免 scale 计算除零
    }

    private static void Release(object comObject)
    {
        try
        {
            if (Marshal.IsComObject(comObject))
                Marshal.ReleaseComObject(comObject);
        }
        catch (Exception)
        {
            // 释放失败不值得打扰用户
        }
    }

    // ---------------- COM 定义（vtable 顺序与 Windows SDK 一致，勿改序） ----------------

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);

        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice? endpoint);

        int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice? device);

        int RegisterEndpointNotificationCallback(IntPtr client);

        int UnregisterEndpointNotificationCallback(IntPtr client);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        int Activate(ref Guid iid, int clsCtx, IntPtr activationParams,
            [MarshalAs(UnmanagedType.IUnknown)] out object? instance);

        int OpenPropertyStore(int access, out IntPtr store);

        int GetId([MarshalAs(UnmanagedType.LPWStr)] out string? id);

        int GetState(out int state);
    }

    [ComImport, Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioClient
    {
        int Initialize(int shareMode, int streamFlags, long bufferDuration, long periodicity, IntPtr format, IntPtr sessionGuid);

        int GetBufferSize(out int frames);

        int GetStreamLatency(out long latency);

        int GetCurrentPadding(out int frames);

        int IsFormatSupported(int shareMode, IntPtr format, out IntPtr closestMatch);

        int GetMixFormat(out IntPtr format);

        int GetDevicePeriod(out long defaultPeriod, out long minimumPeriod);

        int Start();

        int Stop();

        int Reset();

        int SetEventHandle(IntPtr handle);

        int GetService(ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object? service);
    }

    [ComImport, Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioCaptureClient
    {
        int GetBuffer(out IntPtr data, out int frames, out int flags, out long devicePosition, out long qpcPosition);

        int ReleaseBuffer(int frames);

        int GetNextPacketSize(out int frames);
    }
}
