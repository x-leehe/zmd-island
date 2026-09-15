using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using EndfieldCharge.Contracts;
using EndfieldCharge.Contracts.Avalonia;
using EndfieldCharge.Host.Island;
using EndfieldCharge.Services;

namespace EndfieldCharge.Host.Plugins.Music;

/// <summary>
/// 音乐插件（外部 DLL，自带一切）：
///   · IIslandSkin          —— 三态视觉树与动画；
///   · IIslandExpandToggle  —— 岛内「展开 / 收起」按钮（宿主据此切换 等待态 ↔ 展开态）；
///   · IContextMenuContributor —— 岛右键菜单的 上一曲 / 下一曲 / 暂停·播放；
///   · 岛内按钮 → 媒体源（播放/暂停、上/下一曲）；
///   · 自带 SMTC 数据源：事件驱动（会话/曲目/播放态/seek 由 SMTC 事件推送，不轮询 SMTC），
///     进度用 250ms 本地插值 tick 平滑，且只在「正在播放 **且** 岛可见」时干活。
/// 宿主只按契约认识它，不引用本类型。
/// </summary>
public sealed class MusicPlugin : IPlugin, IIslandSkin, IIslandExpandToggle, IContextMenuContributor, IPluginSettingsPage
{
    /// <summary>
    /// 启动即连接 SMTC？
    /// <c>true</c> = 「音乐开始播放 → 自动展开岛」从进程启动起就生效；
    /// <c>false</c> = 懒连接（首次切到音乐岛才连；在那之前完全不碰 SMTC，但也就无法感知播放开始）。
    /// </summary>
    private const bool ConnectOnStartup = true;

    /// <summary>自动展开冷却：避免 SMTC 事件抖动导致连弹。</summary>
    private static readonly TimeSpan AutoExpandCooldown = TimeSpan.FromSeconds(5);

    private readonly MusicIslandView _view = new();
    private readonly SmtcMediaSource _media = new(ConnectOnStartup);
    private readonly DispatcherTimer _progressTimer;
    private string _dataDirectory = string.Empty;

    private IPluginContext? _context;
    private MusicSettings _settings = new();

    private readonly LyricsService _lyrics;

    /// <summary>最近一次造的 LRCLIB provider（可能为 null：来源里不含 LRCLIB）；改缓存上限时同步给它。</summary>
    private LrclibProvider? _lrclib;

    /// <summary>当前这一帧（频谱闸门要看它的来源 AUMID 与播放态）。</summary>
    private MusicFrame _frame = MusicFrame.Empty;

    /// <summary>频谱：分析器是纯计算；采集器**只在白名单放行时才创建**。</summary>
    private const int MinVisualizerBars = 24;
    private const int MaxVisualizerBars = 96;

    private SpectrumAnalyzer _analyzer = new();
    private readonly float[] _sampleBuffer = new float[1024];
    private readonly DispatcherTimer _spectrumTimer;
    private AudioLoopbackCapture? _capture;
    private bool _spectrumAtFloor;

    /// <summary>假动画的相位来源：用**真实经过时间**，这样换 tick 间隔也不会改变动画速度。</summary>
    private readonly Stopwatch _spectrumClock = Stopwatch.StartNew();

    private TimeSpan _lastSpectrumTick;

    /// <summary>三根柱是否已经停在"暂停"状态（停住就不再刷）。</summary>
    private bool _pulseAtFloor = true;

    /// <summary>上一次记频谱峰值的时间（节流到 5s 一行）。</summary>
    private DateTime _lastSpectrumLog = DateTime.MinValue;

    /// <summary>见过的来源（AUMID）。只用于列出白名单候选，上限防止无限增长。</summary>
    private readonly HashSet<string> _knownSources = new(StringComparer.OrdinalIgnoreCase);

    private const int KnownSourcesCap = 32;
    private bool _wasPlaying;
    private bool _baselineSet;          // 首帧只作基线：启动时本来就在播不该弹岛
    private DateTime _lastAutoExpandUtc;

    public MusicPlugin()
    {
        // 展开按钮转发（放构造器：Initialize 由 PluginLoader 调用，但转发本身不依赖上下文）
        _view.ExpandToggled += () => ExpandToggled?.Invoke();

        // 岛内「播放 / 暂停、上 / 下一曲」→ 媒体源（真实状态由 SMTC 事件回填）
        _view.ActionRequested += action => _ = action switch
        {
            MusicAction.PlayPause => _media.PlayPauseAsync(),
            MusicAction.Next => _media.NextAsync(),
            MusicAction.Previous => _media.PreviousAsync(),
            _ => Task.CompletedTask,
        };

        // 本地进度插值（250ms）：不访问 SMTC；暂停或岛不可见时由 TickProgress 直接跳过
        _progressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _progressTimer.Tick += (_, _) => TickProgress();

        // 事件驱动：SMTC 一有变化就绑定（含"无会话"空态与"未连接"降级态）
        _media.FrameChanged += OnFrameChanged;

        // 枚举到的来源（含未授权）→ 「见过的来源」候选；授权与否只由白名单决定
        _media.SourcesObserved += OnSourcesObserved;

        // 歌词：来源由设置决定；换曲在 OnFrameChanged 里发起，拿到结果后回 UI 线程刷新那一行
        _lyrics = new LyricsService(CreateLyricsProvider);
        _lyrics.Changed += OnLyricsChanged;

        // 频谱：60fps（30fps 的观感偏"卡"；平滑已改成时间常数，提速不会让手感变硬）
        _spectrumTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _spectrumTimer.Tick += (_, _) => TickSpectrum();
        _spectrumTimer.Start();
    }

    /// <summary>内容变化 → 绑定；「开始播放」的上升沿请宿主把岛以展开态弹出。</summary>
    private void OnFrameChanged(MusicFrame frame)
    {
        _frame = frame;

        if (!_baselineSet)
        {
            // 首帧只作基线（启动时本来就在播 → 不弹）
            _baselineSet = true;
            _wasPlaying = frame.IsPlaying;
        }
        else if (frame.IsPlaying && !_wasPlaying)
        {
            _wasPlaying = true;
            TryAutoExpand(frame);
        }
        else
        {
            _wasPlaying = frame.IsPlaying;
        }

        _view.BindMusic(frame);
        RefreshLyric();                                    // 换曲先按"暂无歌词"刷一次：立刻回到曲名占位，不留上一首的残影
        _ = _lyrics.OnTrackChangedAsync(ToQuery(frame));    // 同曲会被去重，只有换曲才真的去取

        // 记下见过的来源：白名单界面据此列出候选（只记录，不代表被允许）
        if (!string.IsNullOrWhiteSpace(frame.SourceAppId) && _knownSources.Count < KnownSourcesCap)
            _knownSources.Add(frame.SourceAppId);

        if (frame.IsPlaying)
            _progressTimer.Start();
        else
            _progressTimer.Stop();
    }

    /// <summary>枚举到的来源（含未授权）→ 记入「见过的来源」候选；只用于设置页展示，不代表已允许。</summary>
    private void OnSourcesObserved(IReadOnlyList<string> ids)
    {
        foreach (var id in ids)
        {
            if (_knownSources.Count >= KnownSourcesCap)
                return;

            _knownSources.Add(id);
        }
    }

    /// <summary>
    /// 音乐开始播放 → 请宿主切到本皮肤并以展开态弹出（左键单击可立即隐藏；岛内按钮自带
    /// <c>e.Handled</c>，交互功能不受影响）。已连接才可能被触发：懒连接模式下要先用过一次音乐岛。
    /// </summary>
    private void TryAutoExpand(MusicFrame frame)
    {
        // 白名单（B）：不在名单里的播放器**连请求都不发** —— 既不采集，也不主动弹岛。
        // 默认空名单 ⇒ 一律不弹；用户显式加入后才会有主动行为。
        if (!SpectrumWhitelist.IsAllowed(_settings.SpectrumSources, frame.SourceAppId))
        {
            Logger.Info($"Music: 来源不在白名单（{frame.SourceAppId ?? "未知"}）→ 不主动弹岛");
            return;
        }

        if (DateTime.UtcNow - _lastAutoExpandUtc < AutoExpandCooldown)
            return;

        // 服务在插件 Initialize 之后才可用（HudWindow 晚于插件加载创建），故此处按需取
        var host = _context?.GetService<IIslandHost>();
        if (host is null)
            return;

        if (!host.ShowExpanded(Id))
            return;

        _lastAutoExpandUtc = DateTime.UtcNow;
        Logger.Info("Music: 检测到开始播放 → 展开音乐岛");
    }

    /// <summary>本地进度插值 tick：岛不可见（未激活 / 已隐藏）时不做任何视觉刷新。</summary>
    private void TickProgress()
    {
        if (!_view.IsEffectivelyVisible)
            return;

        var frame = _media.Interpolate();
        _view.UpdateProgress(frame.Progress);
        _view.UpdateLyric(_lyrics.CurrentAt(LyricPosition(frame)));
    }

    // ---------------- 歌词 ----------------

    /// <summary>
    /// 按来源造 provider；<c>off</c> 与未知来源返回 null ⇒ 不取也不显示。
    /// <para>
    /// <para>
    /// <c>merge</c> = **三源并行择优**：LRCLIB / 网易云 / 本地同时探测，汇总取最匹配的一条；
    /// <c>prefer-*</c> = **串行回退**：把该源排第一、依次问下去，第一个给出歌词的就用（不比较谁更像）；
    /// <c>lrclib</c> / <c>netease</c> / <c>local</c> = 只用这一个源；<c>off</c> = 关闭歌词。
    /// </para>
    /// </summary>
    private ILyricsProvider? CreateLyricsProvider(string source)
    {
        var lyricsDirectory = Path.Combine(_dataDirectory, "lyrics");
        var lrclib = new LrclibProvider(lyricsDirectory, cacheLimitBytes: LyricsCache.ToBytes(_settings.LyricsCacheLimitMegabytes));
        _lrclib = lrclib;
        var netease = new NeteaseProvider();
        var local = new LocalLrcProvider(lyricsDirectory);

        return source switch
        {
            "merge" => new MergedLyricsProvider(lrclib, netease, local),
            "prefer-lrclib" => new FallbackLyricsProvider(lrclib, netease, local),
            "prefer-netease" => new FallbackLyricsProvider(netease, lrclib, local),
            "prefer-local" => new FallbackLyricsProvider(local, lrclib, netease),
            "lrclib" => lrclib,
            "netease" => netease,
            "local" => local,
            _ => null,
        };
    }

    /// <summary>歌词查询指纹：曲名 + 艺术家 + 专辑 + 时长。</summary>
    private static LyricsQuery ToQuery(MusicFrame frame) => new(
        frame.Title ?? string.Empty,
        frame.Artist ?? string.Empty,
        frame.Album ?? string.Empty,
        frame.Duration);

    /// <summary>绝对播放位置：帧里只有 0..1 的进度，乘总时长即得（歌词按绝对时间取行）。</summary>
    private static TimeSpan LyricPosition(MusicFrame frame) =>
        frame.Duration > TimeSpan.Zero
            ? TimeSpan.FromSeconds(frame.Progress * frame.Duration.TotalSeconds)
            : TimeSpan.Zero;

    /// <summary>歌词就绪（或换曲清空）→ 回 UI 线程刷新。</summary>
    private void OnLyricsChanged()
    {
        if (Dispatcher.UIThread.CheckAccess())
            RefreshLyric();
        else
            Dispatcher.UIThread.Post(RefreshLyric);
    }

    private void RefreshLyric()
    {
        var frame = _media.Interpolate();
        _view.UpdateLyric(_lyrics.CurrentAt(LyricPosition(frame)));
    }

    // ---------------- 频谱（白名单是采集的前置条件） ----------------

    /// <summary>
    /// 30fps：把「在播放」的标示与「真实频谱采集」分开处理 ——
    /// <list type="bullet">
    ///   <item>三根柱的假动画只看「岛可见 + 在播放」，**与白名单无关**（它只是播放标示，不碰音频）；</item>
    ///   <item>真实采集要过白名单（硬约束），只喂给展开态的背景层。</item>
    /// </list>
    /// </summary>
    private void TickSpectrum()
    {
        var now = _spectrumClock.Elapsed;
        double dt = Math.Clamp((now - _lastSpectrumTick).TotalSeconds, 0.001d, 0.25d);
        _lastSpectrumTick = now;

        bool visible = _view.IsEffectivelyVisible;
        bool pulse = visible && _frame.IsPlaying;
        if (pulse || !_pulseAtFloor)
        {
            _view.UpdatePlayIndicator(pulse, now.TotalSeconds);
            _pulseAtFloor = !pulse;
        }

        // 长句歌词"跟唱"：**分页式** —— 这一屏快唱完了才缓缓 unfold 出后面一段，
        // 绝大多数时间不动（progress 由 LRC 的时间轨道给出）。暂停时不推进。
        if (visible && _frame.IsPlaying)
            _view.UpdateLyricFollow(_lyrics.ProgressAt(LyricPosition(_media.Interpolate())));

        bool allowed =
            visible &&
            _settings.ShowVisualizer &&
            _frame.IsPlaying &&
            SpectrumWhitelist.IsAllowed(_settings.SpectrumSources, _frame.SourceAppId);

        if (!allowed)
        {
            StopCapture(visible ? "闸门关闭" : "岛不可见");

            if (_spectrumAtFloor)
                return;                     // 已经全零，不必再刷（避免无谓的几何重建）

            var decayed = _analyzer.Decay(dt);
            _spectrumAtFloor = IsAtFloor(decayed);
            _view.UpdateSpectrum(decayed);
            return;
        }

        // 只有白名单放行时才会走到这里 —— 采集链路在此之前根本不存在
        if (_capture is null)
        {
            _capture = new AudioLoopbackCapture(_sampleBuffer.Length);
            if (!_capture.Start())
            {
                _capture.Dispose();
                _capture = null;
            }
        }

        if (_capture is null || !_capture.TryReadLatest(_sampleBuffer))
        {
            _view.UpdateSpectrum(_analyzer.Decay(dt));
            return;
        }

        _spectrumAtFloor = false;
        var levels = _analyzer.Process(_sampleBuffer, dt);
        _view.UpdateSpectrum(levels);
        LogSpectrumLevels(levels);
    }

    /// <summary>
    /// 每约 5 秒记一行峰值：背景频谱"看着没动"时，一眼分清是**根本没采到**、
    /// 还是**采到了但幅度太小**（后者要调归一化，前者要查设备）。
    /// </summary>
    private void LogSpectrumLevels(IReadOnlyList<float> levels)
    {
        var now = DateTime.UtcNow;
        if (now - _lastSpectrumLog < TimeSpan.FromSeconds(5))
            return;

        _lastSpectrumLog = now;

        float peak = 0f;
        float floor = float.MaxValue;
        double sum = 0d;
        foreach (var level in levels)
        {
            peak = Math.Max(peak, level);
            floor = Math.Min(floor, level);
            sum += level;
        }

        Logger.Info($"Music: 频谱 峰 {peak:F3} · 均 {(sum / Math.Max(1, levels.Count)):F3} · " +
                    $"最低 {(levels.Count == 0 ? 0f : floor):F3} · 落差 {peak - (levels.Count == 0 ? 0f : floor):F3} · " +
                    $"样本 {(peak > 0.001f ? "有信号" : "静音")}");
    }

    private void StopCapture(string reason)
    {
        if (_capture is null)
            return;

        _capture.Dispose();
        _capture = null;
        Logger.Info($"Music: 频谱采集已停止（{reason}）");
    }

    private static bool IsAtFloor(IReadOnlyList<float> levels)
    {
        foreach (var level in levels)
        {
            if (level > 0.001f)
                return false;
        }

        return true;
    }

    // ---------------- IPlugin ----------------

    public string Id => "music";

    public string DisplayName => "音乐";

    public bool CanUnload => true;

    public string DataDirectory => _dataDirectory;

    public void Initialize(IPluginContext context)
    {
        _dataDirectory = context.DataDirectory;
        _context = context;
        _settings = MusicSettingsStore.Load(_dataDirectory);

        // 旧取值迁移：`smtc` 根本不是有效来源（SMTC 不提供歌词），`auto` 是旧的「顺序回退」。
        // 都迁到 `prefer-lrclib`（两源并行择优）并写回磁盘，否则设置页 / 日志会一直显示已不存在的取值。
        if (_settings.LyricSource is LyricsService.LegacySource or LyricsService.LegacyAutoSource)
        {
            _settings = _settings with { LyricSource = "prefer-lrclib" };
            MusicSettingsStore.Save(_dataDirectory, _settings);
        }

        _lyrics.Use(_settings.LyricSource);
        _media.Whitelist = _settings.SpectrumSources;       // 硬门禁：不在名单里的会话不显示、不控制
        ApplyAnalyzerBands(_settings.VisualizerBars);
        _view.ApplyPreferences(_settings.ShowTitleWhenNoLyric, _settings.ShowVisualizer, _settings.VisualizerIntensity, _settings.VisualizerBars);
        Logger.Info($"Music: 设置已加载（展开态超时 {ExpandedTimeoutSeconds:F1}s（下限 3s） / " +
                    $"无歌词显示歌名 {OnOff(_settings.ShowTitleWhenNoLyric)} / " +
                    $"可视化器 {OnOff(_settings.ShowVisualizer)}（强度 {_settings.VisualizerIntensity:F1}× · 柱数 {_settings.VisualizerBars}） / " +
                    $"歌词来源 {_lyrics.Source} / " +
                    $"歌词缓存上限 {LyricsCache.ClampLimitMegabytes(_settings.LyricsCacheLimitMegabytes)} MB）");
    }

    /// <summary>按设置重建频谱分析器（柱数变了才重建；重建会重置平滑，属设置改动的预期结果）。</summary>
    private void ApplyAnalyzerBands(int bars)
    {
        bars = Math.Clamp(bars, MinVisualizerBars, MaxVisualizerBars);
        if (_analyzer.Bands != bars)
            _analyzer = new SpectrumAnalyzer(bands: bars);
    }

    private static string OnOff(bool value) => value ? "开" : "关";

    public void Shutdown()
    {
        _progressTimer.Stop();
        _spectrumTimer.Stop();
        StopCapture("插件卸载");
        _lyrics.Dispose();
        _media.Dispose();
    }

    // ---------------- IIslandSkin ----------------

    public Control View => _view;

    /// <summary>音乐数据走自身 SMTC，不使用宿主的内容描述符。</summary>
    public void BindContent(IslandContentDescriptor content)
    {
    }

    public Task PlayResponseAsync(CancellationToken ct)
    {
        _media.Start(); // 懒连接模式下：首次激活即开始连接（幂等）
        return _view.PlayResponseAsync(ct);
    }

    public Task PlayWaitingAsync(CancellationToken ct)
    {
        _media.Start();
        return _view.PlayWaitingAsync(ct);
    }

    public Task PlayContractAsync(CancellationToken ct) => _view.PlayContractAsync(ct);

    public Task PlayDismissAsync(CancellationToken ct) => _view.PlayDismissAsync(ct);

    public void ApplyScale(double globalScale) => _view.ApplyScale(globalScale);

    public IslandMetrics CurrentMetrics => _view.CurrentMetrics;

    public IslandMetrics HoverMetrics => _view.HoverMetrics;

    public void PrepareRevealStart() => _view.PrepareRevealStart();

    // ---------------- 皮肤特性（覆写 IIslandSkin 的默认接口成员） ----------------

    /// <summary>数据自给（SMTC），不使用宿主电池内容。</summary>
    public bool UsesHostContent => false;

    /// <summary>窗口加高，容纳 560×160 展开态。</summary>
    public double WindowHeight => 220d;

    /// <summary>展开态超时（秒）：最低 3——每个状态都必须有生命周期超时。</summary>
    public double ExpandedTimeoutSeconds => Math.Max(3d, _settings.ExpandedTimeoutSeconds);

    // ---------------- IIslandExpandToggle ----------------

    public event Action? ExpandToggled;

    // ---------------- IContextMenuContributor ----------------

    public IEnumerable<MenuContribution> GetMenuContributions(MenuContext context)
    {
        if (context.Target != MenuTarget.Island)
            yield break;

        yield return Item("music.prev", Localization.MediaPrev, 0, () => _ = _media.PreviousAsync());
        yield return Item("music.next", Localization.MediaNext, 1, () => _ = _media.NextAsync());
        // 每次打开菜单时重新取一次实时播放态（菜单是每次右键重建的），文案才与实际行为一致
        yield return Item(
            "music.playpause",
            _media.IsPlaying ? Localization.MediaPause : Localization.MediaPlay,
            2,
            () => _ = _media.PlayPauseAsync());
    }

    private static MenuContribution Item(string id, string header, int priority, Action command) => new()
    {
        Id = id,
        Header = header,
        Target = MenuTarget.Island,
        Section = MenuSection.Content,
        Priority = priority,
        Command = command,
    };

    // ---------------- IPluginSettingsPage ----------------

    public string SettingsTitle => DisplayName;

    public Control CreateSettingsView() => new MusicSettingsView(
        () => _settings,
        UpdateSettings,
        () => KnownSources,
        () => Path.Combine(_dataDirectory, "lyrics"));

    /// <summary>见过的来源（AUMID）：设置面板据此列出可点选加入白名单的候选。</summary>
    public IReadOnlyList<string> KnownSources
    {
        get
        {
            var list = new List<string>(_knownSources);
            list.Sort(StringComparer.OrdinalIgnoreCase);
            return list;
        }
    }

    /// <summary>写回设置：持久化到插件数据目录，并立即作用到岛的视觉。</summary>
    private void UpdateSettings(MusicSettings settings)
    {
        _settings = settings;
        MusicSettingsStore.Save(_dataDirectory, settings);
        _lyrics.Use(settings.LyricSource);                  // 换来源 → 丢掉旧歌词，等下一次换曲重取
        _media.Whitelist = settings.SpectrumSources;         // 白名单变更 → 立刻重挑会话（未授权的不显示、不控制）
        ApplyAnalyzerBands(settings.VisualizerBars);
        _view.ApplyPreferences(settings.ShowTitleWhenNoLyric, settings.ShowVisualizer, settings.VisualizerIntensity, settings.VisualizerBars);
        RefreshLyric();                                     // ApplyPreferences 会重绑帧，歌词要再写一次
        ApplyLyricsCacheLimit(settings.LyricsCacheLimitMegabytes);  // 调低上限 → 立即回收，无需重启
    }

    /// <summary>
    /// 把新的歌词缓存上限作用到当前 provider **和**磁盘：
    /// provider 可能没重建（歌词来源没变），也可能根本不是 LRCLIB（如「仅本地」），
    /// 所以除了同步 provider 的字段，还直接对缓存目录回收一次，保证设置立刻生效。
    /// </summary>
    private void ApplyLyricsCacheLimit(int megabytes)
    {
        long limitBytes = LyricsCache.ToBytes(megabytes);

        _lrclib?.ApplyCacheLimit(limitBytes);               // 后续每次写入都按新上限回收

        var (deleted, freed) = LyricsCache.Prune(Path.Combine(_dataDirectory, "lyrics"), limitBytes);
        if (deleted > 0)
        {
            Logger.Info($"Music: 歌词缓存上限改为 {LyricsCache.ClampLimitMegabytes(megabytes)} MB → " +
                        $"回收 {deleted} 个 / 释放 {LyricsCache.FormatSize(freed)}");
        }
    }
}
