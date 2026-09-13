using System;
using System.Collections.Generic;
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
public sealed class MusicPlugin : IPlugin, IIslandSkin, IIslandExpandToggle, IContextMenuContributor
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
    }

    /// <summary>内容变化 → 绑定；「开始播放」的上升沿请宿主把岛以展开态弹出。</summary>
    private void OnFrameChanged(MusicFrame frame)
    {
        if (!_baselineSet)
        {
            // 首帧只作基线（启动时本来就在播 → 不弹）
            _baselineSet = true;
            _wasPlaying = frame.IsPlaying;
        }
        else if (frame.IsPlaying && !_wasPlaying)
        {
            _wasPlaying = true;
            TryAutoExpand();
        }
        else
        {
            _wasPlaying = frame.IsPlaying;
        }

        _view.BindMusic(frame);

        if (frame.IsPlaying)
            _progressTimer.Start();
        else
            _progressTimer.Stop();
    }

    /// <summary>
    /// 音乐开始播放 → 请宿主切到本皮肤并以展开态弹出（左键单击可立即隐藏；岛内按钮自带
    /// <c>e.Handled</c>，交互功能不受影响）。已连接才可能被触发：懒连接模式下要先用过一次音乐岛。
    /// </summary>
    private void TryAutoExpand()
    {
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

        _view.UpdateProgress(_media.Interpolate().Progress);
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
    }

    public void Shutdown()
    {
        _progressTimer.Stop();
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

    /// <summary>常驻等待态：不被宿主空闲计时器收缩 / 隐藏。</summary>
    public bool AutoIdleTimeout => false;

    /// <summary>窗口加高，容纳 560×160 展开态。</summary>
    public double WindowHeight => 220d;

    // ---------------- IIslandExpandToggle ----------------

    public event Action? ExpandToggled;

    // ---------------- IContextMenuContributor ----------------

    public IEnumerable<MenuContribution> GetMenuContributions(MenuContext context)
    {
        if (context.Target != MenuTarget.Island)
            yield break;

        yield return Item("music.prev", Localization.MediaPrev, 0, () => _ = _media.PreviousAsync());
        yield return Item("music.next", Localization.MediaNext, 1, () => _ = _media.NextAsync());
        yield return Item("music.playpause", Localization.MediaPause, 2, () => _ = _media.PlayPauseAsync());
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
}
