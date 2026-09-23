using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using EndfieldCharge.Contracts;
using EndfieldCharge.Services;
using Windows.Media.Control;

namespace EndfieldCharge.Host.Plugins.Music;

/// <summary>SMTC 连接状态。</summary>
public enum SmtcConnectionState
{
    /// <summary>正在连接（含退避重试中）。</summary>
    Connecting,

    /// <summary>已连接：之后完全由事件驱动，不再轮询。</summary>
    Connected,

    /// <summary>快速重试已用尽，降级为不可用（岛内明确提示），仅保留慢速自愈。</summary>
    Unavailable,
}

/// <summary>
/// SMTC（Windows 媒体会话）数据源——<strong>事件驱动</strong>：
/// <list type="bullet">
/// <item>连接：<c>RequestAsync()</c> 失败按 5s 退避重试，12 次（≈1 分钟）后降级为
/// <see cref="SmtcConnectionState.Unavailable"/>（岛内提示「未连接 SMTC」），之后每 60s 慢速自愈；
/// 成功则彻底停表。</item>
/// <item>变化：订阅 manager 的 <c>SessionsChanged</c> / <c>CurrentSessionChanged</c> 与 session 的
/// <c>MediaPropertiesChanged</c> / <c>PlaybackInfoChanged</c> / <c>TimelinePropertiesChanged</c>，
/// 事件一到才重读一帧并经 <see cref="FrameChanged"/> 发布——不再每秒轮询 SMTC。</item>
/// <item>进度：<c>TimelinePropertiesChanged</c> 只在 seek 时触发，平滑推进由
/// <see cref="Interpolate"/> 本地插值（位置 + 经过时间 × 速率，不访问 SMTC）。</item>
/// </list>
/// 无会话 → 空态（<see cref="MusicFrame.Empty"/>，不显示假数据）。
/// </summary>
public sealed class SmtcMediaSource : IDisposable
{
    private static readonly TimeSpan FastRetry = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan SlowRetry = TimeSpan.FromSeconds(60);
    private const int MaxFastAttempts = 12;

    private readonly DispatcherTimer _connectTimer;
    private readonly HashSet<GlobalSystemMediaTransportControlsSession> _attached = new();

    private int _attempts;
    private bool _connecting;
    private bool _started;
    private bool _disposed;

    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private IReadOnlyList<string>? _whitelist;

    private MusicFrame _frame = MusicFrame.Empty;

    // ---- 本地进度插值基准（仅 ResyncAsync 写，Interpolate 读） ----
    private TimeSpan _position;
    private TimeSpan _duration;
    private double _rate = 1d;
    private DateTime _stampUtc = DateTime.UtcNow;

    // ---- 封面：当前 + 上一张（等 UI 换绑后再释放） ----
    private Bitmap? _cover;
    private Bitmap? _previousCover;
    private string? _coverKey;

    /// <param name="connectOnStartup">
    /// <c>true</c> = 构造即连接；<c>false</c> = 懒连接（由 <see cref="Start"/> 触发，例如皮肤首次激活）。
    /// </param>
    public SmtcMediaSource(bool connectOnStartup)
    {
        _connectTimer = new DispatcherTimer { Interval = FastRetry };
        _connectTimer.Tick += (_, _) => _ = ConnectAsync();

        if (connectOnStartup)
            Start();
    }

    /// <summary>开始连接（幂等）：可构造函数即调（启动即连），或在皮肤首次激活时调（懒连接）。</summary>
    public void Start()
    {
        if (_disposed || _started)
            return;

        _started = true;
        _connectTimer.Start();
        _ = ConnectAsync(); // 立即尝试一次
    }

    /// <summary>连接状态。</summary>
    public SmtcConnectionState State { get; private set; } = SmtcConnectionState.Connecting;

    /// <summary>当前是否正在播放（宿主据此决定要不要跑进度插值 tick）。</summary>
    public bool IsPlaying => _frame.IsPlaying;

    /// <summary>内容变化：连接状态 / 曲目 / 封面 / 播放态 / seek，以及空态与不可用降级态。</summary>
    public event Action<MusicFrame>? FrameChanged;

    /// <summary>
    /// 枚举到的所有来源 AUMID（含**未被允许**的）—— 仅供设置页列出「见过的来源」候选，
    /// <strong>不代表已允许</strong>；授权与否只由 <see cref="Whitelist"/> 决定。
    /// </summary>
    public event Action<IReadOnlyList<string>>? SourcesObserved;

    /// <summary>
    /// 允许的会话来源白名单（AUMID），**硬门禁**：不在名单里的会话既不会被显示，也不会被控制；
    /// 空名单 / 取不到 AUMID 一律不允许。赋值（含清空）会立即重新挑选会话并刷新显示。
    /// </summary>
    public IReadOnlyList<string>? Whitelist
    {
        get => _whitelist;
        set
        {
            _whitelist = value;
            Resync();
        }
    }

    /// <summary>本地插值出当前帧：播放中按 <c>位置 + 经过时间 × 速率</c> 推进，纯本地计算（不访问 SMTC）。</summary>
    public MusicFrame Interpolate()
    {
        if (!_frame.IsPlaying || _duration <= TimeSpan.Zero)
            return _frame;

        var pos = _position + TimeSpan.FromSeconds((DateTime.UtcNow - _stampUtc).TotalSeconds * _rate);
        if (pos < TimeSpan.Zero)
            pos = TimeSpan.Zero;
        else if (pos > _duration)
            pos = _duration;

        return _frame with { Progress = pos.TotalSeconds / _duration.TotalSeconds };
    }

    // ---------------- 传输控制 ----------------

    public async Task PlayPauseAsync()
    {
        var s = _session;
        if (s is null) return;
        try { await s.TryTogglePlayPauseAsync(); }
        catch (Exception ex) { Logger.Warn($"SMTC 播放暂停失败：{ex.Message}"); }
    }

    public async Task NextAsync()
    {
        var s = _session;
        if (s is null) return;
        try { await s.TrySkipNextAsync(); }
        catch (Exception ex) { Logger.Warn($"SMTC 下一曲失败：{ex.Message}"); }
    }

    public async Task PreviousAsync()
    {
        var s = _session;
        if (s is null) return;
        try { await s.TrySkipPreviousAsync(); }
        catch (Exception ex) { Logger.Warn($"SMTC 上一曲失败：{ex.Message}"); }
    }

    // ---------------- 连接（失败的退避重试 + 封顶后的慢速自愈） ----------------

    private async Task ConnectAsync()
    {
        if (_disposed || _connecting || _manager is not null)
            return;

        _connecting = true;
        try
        {
            var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            if (manager is null)
            {
                OnConnectFailed("RequestAsync 返回 null");
                return;
            }

            _manager = manager;

            // 事件驱动：会话集 / 当前会话一变就重新同步（"先开机、后开播放器"由此覆盖）
            _manager.SessionsChanged += (_, _) => Resync();
            _manager.CurrentSessionChanged += (_, _) => Resync();

            State = SmtcConnectionState.Connected;
            _connectTimer.Stop();
            Logger.Info("Music: SMTC 已连接（事件驱动，不再轮询）");

            await ResyncAsync(); // 立刻同步一帧（可能为"无会话"空态）
        }
        catch (Exception ex)
        {
            OnConnectFailed(ex.Message);
        }
        finally
        {
            _connecting = false;
        }
    }

    private void OnConnectFailed(string reason)
    {
        _attempts++;

        if (_attempts < MaxFastAttempts)
        {
            // 只在前两次留痕，之后静默重试（避免刷屏）
            if (_attempts <= 2)
                Logger.Warn($"SMTC 连接失败（第 {_attempts}/{MaxFastAttempts} 次，{FastRetry.TotalSeconds:F0}s 后重试）：{reason}");
            return;
        }

        if (State != SmtcConnectionState.Unavailable)
        {
            State = SmtcConnectionState.Unavailable;
            _connectTimer.Interval = SlowRetry; // 降级后只做慢速自愈
            Logger.Warn($"SMTC 快速重试已用尽（{MaxFastAttempts} 次），降级为不可用；之后每 {SlowRetry.TotalSeconds:F0}s 静默自愈。原因：{reason}");
            SetFrame(MusicFrame.Unavailable());
            return;
        }

        // 慢速自愈失败：静默（避免每分钟一条噪声）
    }

    // ---------------- 事件 → 重新同步 ----------------

    /// <summary>任何 SMTC 事件都走这里：回 UI 线程重读一帧（事件可能在线程池/MTA 上触发）。</summary>
    private void Resync()
    {
        if (_disposed)
            return;

        if (Dispatcher.UIThread.CheckAccess())
            _ = ResyncAsync();
        else
            Dispatcher.UIThread.Post(() => _ = ResyncAsync());
    }

    private async Task ResyncAsync()
    {
        if (_disposed || _manager is null)
            return;

        ReportObservedSources();

        try
        {
            var session = PickSession();
            _session = session;
            AttachSession(session);

            if (session is null)
            {
                _duration = TimeSpan.Zero;
                _position = TimeSpan.Zero;
                SetFrame(MusicFrame.Empty); // 无会话：空态
                return;
            }

            var props = await session.TryGetMediaPropertiesAsync();
            var timeline = session.GetTimelineProperties();
            var info = session.GetPlaybackInfo();

            _duration = timeline.EndTime - timeline.StartTime;
            if (_duration < TimeSpan.Zero)
                _duration = TimeSpan.Zero;

            _position = timeline.Position - timeline.StartTime;
            if (_position < TimeSpan.Zero)
                _position = TimeSpan.Zero;

            _rate = ReadRate(info);
            _stampUtc = DateTime.UtcNow;

            // 先出文字：封面解码可能要几十到几百毫秒，**不能让它挡住标题 / 进度 / 播放态**
            var frame = new MusicFrame
            {
                Title = props.Title,
                Artist = props.Artist,
                Album = props.AlbumTitle,
                Duration = _duration,
                SourceAppId = ReadSourceAppId(session),
                LyricCurrent = null, // 歌词由插件的 LyricsService 覆写（按播放位置取行）
                Progress = _duration > TimeSpan.Zero
                    ? Math.Clamp(_position.TotalSeconds / _duration.TotalSeconds, 0d, 1d)
                    : 0d,
                IsPlaying = info.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                Cover = null,        // 见下：拿到后再补一帧
            };
            SetFrame(frame);

            // 封面备好后再补一帧（同曲缓存命中时几乎立刻返回）
            var cover = await LoadCoverAsync(props);

            // 期间可能已经换曲 / 重扫：只在这帧仍是当前帧时补图，免得把旧封面盖到新曲上
            if (ReferenceEquals(_frame, frame))
                SetFrame(frame with { Cover = cover });
        }
        catch (Exception ex)
        {
            Logger.Warn($"SMTC 读取失败（{ex.GetType().Name}）：{ex.Message}");
            SetFrame(MusicFrame.Empty);
        }
    }

    /// <summary>
    /// 在**白名单允许**的会话里挑一个：当前在播 → 任意在播 → 当前 → 任意允许，全不允许则 <c>null</c>
    /// （调用方据此显示空态，而不是退回未授权的当前会话）。挑选规则见 <see cref="SessionSelection"/>。
    /// </summary>
    private GlobalSystemMediaTransportControlsSession? PickSession()
    {
        var manager = _manager;
        if (manager is null)
            return null;

        var current = manager.GetCurrentSession();

        var sessions = new List<GlobalSystemMediaTransportControlsSession>();
        try
        {
            foreach (var s in manager.GetSessions())
                sessions.Add(s);
        }
        catch (Exception ex)
        {
            Logger.Warn($"SMTC 枚举会话失败：{ex.Message}");
        }

        if (current is not null && !sessions.Contains(current))
            sessions.Insert(0, current);

        var candidates = new List<SessionSelection.Candidate>(sessions.Count);
        foreach (var session in sessions)
            candidates.Add(new SessionSelection.Candidate(
                ReadSourceAppId(session), IsPlayingSession(session), ReferenceEquals(session, current)));

        int index = SessionSelection.Pick(candidates, _whitelist);
        return index >= 0 ? sessions[index] : null;
    }

    /// <summary>把枚举到的所有来源 AUMID 报给上游（设置页据此列出「见过的来源」候选），含未授权的。</summary>
    private void ReportObservedSources()
    {
        var handler = SourcesObserved;
        var manager = _manager;
        if (handler is null || manager is null)
            return;

        List<string> ids;
        try
        {
            ids = new List<string>();
            foreach (var session in manager.GetSessions())
            {
                var id = ReadSourceAppId(session);
                if (!string.IsNullOrWhiteSpace(id))
                    ids.Add(id);
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"SMTC 枚举来源失败：{ex.Message}");
            return;
        }

        if (Dispatcher.UIThread.CheckAccess())
            handler(ids);
        else
            Dispatcher.UIThread.Post(() => handler(ids));
    }

    private static bool IsPlayingSession(GlobalSystemMediaTransportControlsSession? session)
    {
        try
        {
            return session is not null
                && session.GetPlaybackInfo().PlaybackStatus
                    == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>播放速率（倍速）。类型在不同 SDK 投影下可能是 double / double?，用装箱兜住两种。</summary>
    private static double ReadRate(GlobalSystemMediaTransportControlsSessionPlaybackInfo info)
    {
        try
        {
            object? raw = info.PlaybackRate;
            return raw is double d && d > 0d ? d : 1d;
        }
        catch
        {
            return 1d;
        }
    }

    /// <summary>
    /// 来源应用的 AUMID（频谱白名单 / 弹岛判定用）。与 <see cref="ReadRate"/> 同理：
    /// 个别 SDK 投影下该属性可能不可用，取不到就返回 null（视为"来源未知"），绝不让建帧失败。
    /// </summary>
    private static string? ReadSourceAppId(GlobalSystemMediaTransportControlsSession session)
    {
        try
        {
            var value = session.SourceAppUserModelId;
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 挂钩会话事件：同一会话只挂一次（用集合去重，避免会话来回切换时重复订阅）。
    /// 订阅用 lambda，无法逐个 <c>-=</c>；<see cref="Dispose"/> 靠 <c>_disposed</c> 守卫让其空转。
    /// </summary>
    private void AttachSession(GlobalSystemMediaTransportControlsSession? session)
    {
        if (session is null || !_attached.Add(session))
            return;

        session.MediaPropertiesChanged += (_, _) => Resync();
        session.PlaybackInfoChanged += (_, _) => Resync();
        session.TimelinePropertiesChanged += (_, _) => Resync();
    }

    private void SetFrame(MusicFrame frame)
    {
        _frame = frame;

        var handler = FrameChanged;
        if (handler is null)
            return;

        if (Dispatcher.UIThread.CheckAccess())
            handler(frame);
        else
            Dispatcher.UIThread.Post(() => handler(frame));
    }

    // ---------------- 封面 ----------------

    /// <summary>封面按「曲目+艺术家+专辑」缓存，避免同曲重复解码。</summary>
    private async Task<Bitmap?> LoadCoverAsync(GlobalSystemMediaTransportControlsSessionMediaProperties props)
    {
        try
        {
            string key = $"{props.Title}|{props.Artist}|{props.AlbumTitle}";
            // 播放器切歌后可能先给出 null，稍后才补上缩略图；不能缓存「无封面」。
            if (key == _coverKey && _cover is not null)
                return _cover;

            if (props.Thumbnail is null)
            {
                _coverKey = key;
                RetireCover(null);
                return null;
            }

            using var ras = await props.Thumbnail.OpenReadAsync();
            using var net = ras.AsStreamForRead();
            var bitmap = new Bitmap(net);

            _coverKey = key;
            RetireCover(bitmap);
            return _cover;
        }
        catch (Exception ex)
        {
            Logger.Warn($"SMTC 封面解码失败：{ex.Message}");
            return _cover;
        }
    }

    /// <summary>
    /// 换封面：旧的那张留到<strong>下一次</strong>换封面时才释放。
    /// 立即释放会让三个 <c>Image.Source</c> 指向已释放的位图（渲染线程可能踩到）。
    /// </summary>
    private void RetireCover(Bitmap? next)
    {
        _previousCover?.Dispose();
        _previousCover = _cover;
        _cover = next;
    }

    public void Dispose()
    {
        _disposed = true;
        _connectTimer.Stop();
        _manager = null;
        _session = null;

        _cover?.Dispose();
        _cover = null;
        _previousCover?.Dispose();
        _previousCover = null;
    }
}
