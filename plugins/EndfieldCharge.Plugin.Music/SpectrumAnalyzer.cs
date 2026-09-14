using System;
using System.Collections.Generic;

namespace EndfieldCharge.Host.Plugins.Music;

/// <summary>
/// 频谱分析：Hann 窗 → radix-2 FFT → **对数分带** → 起落平滑。
/// <para>
/// 纯计算：不碰音频设备、不碰 UI、不依赖任何第三方包，因此可以被单测覆盖。
/// 采集层（WASAPI loopback）只负责把时域样本送进 <see cref="Process"/>。
/// </para>
/// </summary>
public sealed class SpectrumAnalyzer
{
    private const double MinFrequency = 40d;          // 低频起点（更低的部分对观感没意义）
    private const double MaxFrequency = 16000d;       // 高频上限

    /// <summary>
    /// 动态范围地板。这个值有个**两头都不行**的区间，调它之前先看这里：
    /// <list type="bullet">
    ///   <item>太高（-60）：安静段贴近 0、响段很高，落差大 → 观感"生硬、锯齿"；</item>
    ///   <item>太低（-120）：所有段一起被抬到 0.55+ → 变成"贴着岛顶的大平原"；</item>
    ///   <item>-70：安静段 ~0.15、音乐段 ~0.3~0.7 —— 基线低、起伏够，配段内平均 + 邻域平滑才是那个"活着的波形"。</item>
    /// </list>
    /// </summary>
    private const float FloorDb = -70f;

    // 平滑用**时间常数**而不是"每帧比例"：换帧率（30 → 60fps）不会改变手感。
    // 0.17s / 0.53s 等价于旧版 30fps 下的 Attack 0.18 / Release 0.94。
    private const double AttackTau = 0.17d;
    private const double ReleaseTau = 0.53d;

    /// <summary>
    /// 显示域的固定增益：先线性放大，保证最响的那一段落在上沿**附近**而不顶死。
    /// （S 曲线两端都压，没有这一步波就缩成一条小丘；给太大又会把峰值**钳在 1.0** ——
    /// 实测 1.5 配 +3dB/oct 倾斜补偿后出现"峰 1.000 · 最低 0.511"，即整条波贴着顶成平原，故收到 1.2。）
    /// </summary>
    private const double DisplayGain = 1.2d;

    private readonly int _fftSize;
    private readonly int _bands;
    private readonly int _sampleRate;

    private readonly float[] _window;
    private readonly float[] _re;
    private readonly float[] _im;
    private readonly float[] _magnitude;
    private readonly int[] _binStart;
    private readonly int[] _binEnd;
    /// <summary>
    /// 每段的倾斜补偿（dB / 倍频程）。音乐能量天然按 1/f 走（低端强、高端弱），
    /// 不做补偿整条波就往左倒 —— 就是"弹道偏左"。
    /// +3dB/oct 恰好把 1/f 拉平（等价于对每个倍频程做 2 倍振幅补偿），各段高度才均匀。
    /// </summary>
    private const double TiltDbPerOctave = 3d;

    private readonly float[] _levels;
    private readonly float[] _display;
    private readonly float[] _tilt;

    /// <param name="fftSize">FFT 窗口长度（2 的幂）。</param>
    /// <param name="bands">输出频段数（画布稿上展开态是 40 段）。</param>
    /// <param name="sampleRate">采样率（Hz）。</param>
    public SpectrumAnalyzer(int fftSize = 1024, int bands = 40, int sampleRate = 48000)
    {
        if (fftSize < 2 || (fftSize & (fftSize - 1)) != 0)
            throw new ArgumentOutOfRangeException(nameof(fftSize), fftSize, "FFT 长度必须是 2 的幂");
        if (bands < 1)
            throw new ArgumentOutOfRangeException(nameof(bands), bands, "频段数至少为 1");
        if (sampleRate < 8000)
            throw new ArgumentOutOfRangeException(nameof(sampleRate), sampleRate, "采样率过低");

        _fftSize = fftSize;
        _bands = bands;
        _sampleRate = sampleRate;

        _window = new float[fftSize];
        for (int i = 0; i < fftSize; i++)
            _window[i] = (float)(0.5 * (1d - Math.Cos(2d * Math.PI * i / (fftSize - 1))));   // Hann

        _re = new float[fftSize];
        _im = new float[fftSize];
        _magnitude = new float[fftSize / 2];
        _levels = new float[bands];
        _display = new float[bands];
        _tilt = new float[bands];
        _binStart = new int[bands];
        _binEnd = new int[bands];

        BuildBands();
    }

    /// <summary>频段数（= 调用方拿到的数组长度）。</summary>
    public int Bands => _bands;

    /// <summary>需要的样本数（每次 <see cref="Process"/> 的长度）。</summary>
    public int FftSize => _fftSize;

    /// <summary>
    /// 分析一帧时域样本（长度必须等于 <see cref="FftSize"/>），返回**平滑后**的频段值（0..1）。
    /// 返回的是内部数组，调用方不要改它。
    /// </summary>
    /// <param name="samples">一帧时域样本（长度必须等于 <see cref="FftSize"/>）。</param>
    /// <param name="dtSeconds">距上一帧的时长（秒）—— 平滑按时间推进，因此换帧率不影响手感。</param>
    public IReadOnlyList<float> Process(ReadOnlySpan<float> samples, double dtSeconds)
    {
        if (samples.Length != _fftSize)
            throw new ArgumentException($"需要 {_fftSize} 个样本，实际 {samples.Length}", nameof(samples));

        for (int i = 0; i < _fftSize; i++)
        {
            _re[i] = samples[i] * _window[i];
            _im[i] = 0f;
        }

        Transform();

        // 单边幅度谱（1/N 归一；实信号的对称性让直流与奈奎斯特分量各占一半能量）
        for (int i = 0; i < _magnitude.Length; i++)
            _magnitude[i] = MathF.Sqrt(_re[i] * _re[i] + _im[i] * _im[i]) / (_fftSize / 2f);

        for (int band = 0; band < _bands; band++)
        {
            // 段内取**平均**而不是取峰值：峰值会让相邻段互相错开，
            // 曲线高度差突兀（正是"频谱太生硬"的来源之一）。
            double sum = 0d;
            int bins = 0;
            for (int bin = _binStart[band]; bin < _binEnd[band]; bin++)
            {
                sum += _magnitude[bin];
                bins++;
            }

            float magnitude = (bins == 0 ? 0f : (float)(sum / bins)) * _tilt[band];
            float db = 20f * MathF.Log10(magnitude + 1e-9f);
            float target = Math.Clamp((db - FloorDb) / -FloorDb, 0f, 1f);

            _levels[band] = Smooth(_levels[band], target, dtSeconds);
        }

        return ToDisplay();
    }

    /// <summary>
    /// 把线性域的平滑值映射到显示域：先给固定增益（保证最响段能顶到上沿），再套 **S 形曲线**。
    /// **不回写 <see cref="_levels"/>** —— 平滑必须留在线性域，否则曲线会随当前值改变自身斜率。
    /// </summary>
    private float[] ToDisplay()
    {
        for (int band = 0; band < _bands; band++)
        {
            double normalized = Math.Clamp(_levels[band] * DisplayGain, 0d, 1d);
            _display[band] = (float)SpectrumCurve.S(normalized);
        }

        return _display;
    }

    /// <summary>指数趋近：目标高于当前 = 起（<see cref="AttackTau"/>），否则 = 落（<see cref="ReleaseTau"/>）。</summary>
    private static float Smooth(float current, float target, double dtSeconds)
    {
        double tau = target > current ? AttackTau : ReleaseTau;
        double k = 1d - Math.Exp(-Math.Max(1e-4d, dtSeconds) / tau);
        return (float)(current + ((target - current) * k));
    }

    /// <summary>
    /// 没有声音时按时间衰减（暂停 / 岛不可见时调用）—— 否则柱子会卡在最后一帧的高度上。
    /// </summary>
    public IReadOnlyList<float> Decay(double dtSeconds)
    {
        for (int band = 0; band < _bands; band++)
        {
            _levels[band] = Smooth(_levels[band], 0f, dtSeconds);
            if (_levels[band] < 0.001f)
                _levels[band] = 0f;
        }

        return ToDisplay();
    }

    /// <summary>把 40–16000 Hz 按等比划分到各频段，并换算成 FFT bin 区间（跳过直流）。</summary>
    private void BuildBands()
    {
        double nyquist = _sampleRate / 2d;
        double maxFrequency = Math.Min(MaxFrequency, nyquist * 0.98d);
        double ratio = maxFrequency / MinFrequency;
        int maxBin = _magnitude.Length - 1;

        for (int band = 0; band < _bands; band++)
        {
            double low = MinFrequency * Math.Pow(ratio, (double)band / _bands);
            double high = MinFrequency * Math.Pow(ratio, (double)(band + 1) / _bands);

            int start = (int)Math.Floor(low * _fftSize / _sampleRate);
            int end = (int)Math.Ceiling(high * _fftSize / _sampleRate);

            start = Math.Clamp(start, 1, maxBin);
            end = Math.Clamp(end <= start ? start + 1 : end, start + 1, maxBin + 1);

            _binStart[band] = start;
            _binEnd[band] = end;

            // 倾斜补偿：按段中心频率相对最低段抬升（TiltDbPerOctave dB 每倍频程）
            double center = Math.Sqrt(low * high);
            double octaves = Math.Log2(center / MinFrequency);
            _tilt[band] = (float)Math.Pow(10d, TiltDbPerOctave * octaves / 20d);
        }
    }

    /// <summary>原地 radix-2 FFT（Cooley–Tukey，位反转重排 + 蝶形）。</summary>
    private void Transform()
    {
        int n = _fftSize;

        for (int i = 1, j = 0; i < n; i++)
        {
            int bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1)
                j ^= bit;

            j ^= bit;
            if (i >= j)
                continue;

            (_re[i], _re[j]) = (_re[j], _re[i]);
            (_im[i], _im[j]) = (_im[j], _im[i]);
        }

        for (int len = 2; len <= n; len <<= 1)
        {
            double angle = -2d * Math.PI / len;
            float stepRe = (float)Math.Cos(angle);
            float stepIm = (float)Math.Sin(angle);

            for (int start = 0; start < n; start += len)
            {
                float curRe = 1f;
                float curIm = 0f;

                for (int k = 0; k < len / 2; k++)
                {
                    int a = start + k;
                    int b = a + (len / 2);

                    float bRe = (_re[b] * curRe) - (_im[b] * curIm);
                    float bIm = (_re[b] * curIm) + (_im[b] * curRe);

                    _re[b] = _re[a] - bRe;
                    _im[b] = _im[a] - bIm;
                    _re[a] += bRe;
                    _im[a] += bIm;

                    float nextRe = (curRe * stepRe) - (curIm * stepIm);
                    curIm = (curRe * stepIm) + (curIm * stepRe);
                    curRe = nextRe;
                }
            }
        }
    }
}
