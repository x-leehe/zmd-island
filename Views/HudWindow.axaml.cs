using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Threading;
using EndfieldCharge.Animations;
using EndfieldCharge.Contracts;
using EndfieldCharge.Contracts.Avalonia;
using EndfieldCharge.Host.Island;
using EndfieldCharge.Host.Plugins;
using EndfieldCharge.Host.Plugins.Battery;
using EndfieldCharge.Services;
using EndfieldCharge.Settings;

namespace EndfieldCharge.Views;

/// <summary>完整三态动画的文案主题：充电（超充模式）或省电模式。</summary>
public enum HudPlayMode
{
    Charge,
    PowerSaver,
}

/// <summary>
/// HUD 岛窗口 chrome：透明置顶窗口 + 点击穿透 + 定位 + FPS + 右键上下文菜单。
/// 岛的视觉树与动画由电池元插件皮肤（BatteryPlugin / BatteryIslandView）提供，
/// 本类只负责窗口级职责并把播放委托给皮肤。
/// </summary>
public partial class HudWindow : Window, IIslandHost
{
    private CancellationTokenSource? _cts;
    private AppSettings _settings = new();
    private AnimationOptions _animOptions = AnimationOptions.Default;
    private int _fpsFrameCount;
    private DateTime _fpsLastMeasure = DateTime.UtcNow;
    private bool _fpsEnabled;
    private IslandContextMenuWindow? _contextMenu;

    /// <summary>电池元插件：内容映射 + 默认皮肤（从注册表解析，保证与注册实例同一份）。</summary>
    private readonly BatteryPlugin _battery;

    /// <summary>可轮转的岛皮肤（注册顺序 = 轮转顺序，电池在首位；皮肤可用特性把自己排除）。</summary>
    private readonly List<IIslandSkin> _skins;

    /// <summary>当前活动皮肤（电池 / 外部插件，如音乐）。</summary>
    private IIslandSkin _skin = null!;

    /// <summary>切换皮肤中：抑制状态机回调带来的重复播放（SwitchTo 自行驱动一次）。</summary>
    private bool _switching;

    /// <summary>进程内插件注册表（菜单合成 / 皮肤解析）。</summary>
    private readonly IPluginRegistry _registry;

    // ---- 4 态生命周期：状态机（纯逻辑）+ 空闲计时器（本类持有） ----
    private const int HoverDwellMs = 300;   // 隐藏态悬停驻留（进入等待态）

    private readonly IslandStateMachine _sm = new();
    private DispatcherTimer? _idleTimer;      // T1/T2 复用
    private int _idleGen;                     // 代际令牌：使过期回调失效
    private IslandVisualState _idleTarget;    // 计时到期 → 目标状态
    private DateTime? _dwellStarted;          // 隐藏态悬停开始时刻
    private bool _menuOpen;                   // 右键菜单打开（暂停空闲计时）

    // ---- 点击穿透：整窗默认 WS_EX_TRANSPARENT（鼠标穿透到下层窗口），
    //      轮询光标位置，进入岛（胶囊）范围时临时移除该样式使其可交互。 ----
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Win32Point lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct Win32Point
    {
        public int X;
        public int Y;
    }

    private DispatcherTimer? _passThroughTimer;
    private bool? _interactive;

    private void SetInteractive(bool interactive)
    {
        if (_interactive == interactive)
            return;

        var handle = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (handle == IntPtr.Zero)
            return;

        _interactive = interactive;
        var exStyle = GetWindowLong(handle, GWL_EXSTYLE);
        exStyle = interactive
            ? exStyle & ~WS_EX_TRANSPARENT
            : exStyle | WS_EX_TRANSPARENT;
        SetWindowLong(handle, GWL_EXSTYLE, exStyle);
    }

    /// <summary>岛（胶囊）在屏幕上的物理像素包围盒，用于命中判定。
    /// 宽度取自皮肤当前胶囊宽度——收缩态（200）时命中区与悬停区随之收窄。</summary>
    private PixelRect GetIslandBoundsPixels()
    {
        var screen = ResolveScreen(_settings.MonitorIndex);
        double scaling = screen is { Scaling: > 0 } ? screen.Scaling : 1d;
        var m = _skin.CurrentMetrics;
        double gs = Math.Clamp(m.Scale, 0.1, 2d);
        double w = m.Width * gs * scaling;
        double h = m.Height * gs * scaling;
        // 岛顶按皮肤上报的布局偏移对齐（+ 内部缩放原点带来的半量位移），各皮肤锚定一致
        double x = Position.X + Width / 2d * scaling - w / 2d;
        double y = Position.Y + (m.Top + m.Height * (1d - gs) / 2d) * scaling;

        return new PixelRect(
            (int)Math.Round(x),
            (int)Math.Round(y),
            (int)Math.Round(w),
            (int)Math.Round(h));
    }

    /// <summary>岛悬停唤醒区（物理像素）：完整胶囊宽度 560×GlobalScale——不随收缩态收窄，
    /// 悬停在岛完整区域内即保持等待态。</summary>
    private PixelRect GetIslandHoverBoundsPixels()
    {
        var screen = ResolveScreen(_settings.MonitorIndex);
        double scaling = screen is { Scaling: > 0 } ? screen.Scaling : 1d;
        var m = _skin.HoverMetrics;
        double gs = Math.Clamp(m.Scale, 0.1, 2d);
        double w = m.Width * gs * scaling;
        double h = m.Height * gs * scaling;
        double x = Position.X + Width / 2d * scaling - w / 2d;
        double y = Position.Y + (m.Top + m.Height * (1d - gs) / 2d) * scaling;

        return new PixelRect(
            (int)Math.Round(x),
            (int)Math.Round(y),
            (int)Math.Round(w),
            (int)Math.Round(h));
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        SetInteractive(false); // 默认整窗穿透

        _passThroughTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
        _passThroughTimer.Tick += (_, _) =>
        {
            if (!GetCursorPos(out var pt))
                return;

            var clickRect = GetIslandBoundsPixels();       // 当前胶囊宽度（点击穿透命中区）
            var hoverRect = GetIslandHoverBoundsPixels();  // 完整 560 宽（悬停唤醒区）

            if (_sm.Current == IslandVisualState.Hidden)
            {
                // 隐藏态：整窗穿透（岛不可见，无任何可交互区），
                // 悬停唤醒只认屏幕顶缘的细条，避免掠过岛旧区域误触发。
                SetInteractive(false);
                UpdateDwell(IsOverEdgeStrip(pt, clickRect));
                return;
            }

            bool overClick = pt.X >= clickRect.X && pt.X < clickRect.Right
                && pt.Y >= clickRect.Y && pt.Y < clickRect.Bottom;
            SetInteractive(overClick);

            // 悬停权威判定（完整岛宽）：悬停期间 T1/T2 永不触发，收缩态立即回到等待
            bool overHover = pt.X >= hoverRect.X && pt.X < hoverRect.Right
                && pt.Y >= hoverRect.Y && pt.Y < hoverRect.Bottom;
            if (overHover)
            {
                CancelIdle();
                if (_sm.Current == IslandVisualState.Contracted)
                    _sm.TryTransition(IslandVisualState.Waiting);
            }
            else if (!_menuOpen)
            {
                ResumeIdleIfIdleState();
            }
        };
        _passThroughTimer.Start();
    }

    /// <summary>
    /// 屏幕顶缘细条：物理屏幕顶部（Bounds 顶边）起约 3 物理像素高，横向对齐岛区宽度——
    /// 隐藏态悬停唤醒区（驻留 300ms → 等待态）。
    /// 注意：锚定 Bounds 而非 WorkingArea——后者会被顶部任务栏/第三方美化工具（如 MyDockFinder）下移。
    /// </summary>
    private bool IsOverEdgeStrip(Win32Point pt, PixelRect islandRect)
    {
        var screen = ResolveScreen(_settings.MonitorIndex);
        if (screen is null)
            return false;

        double scaling = screen.Scaling > 0 ? screen.Scaling : 1d;
        int stripTop = screen.Bounds.Y;   // 物理屏幕顶部（多显示器时为该屏顶边）
        int stripHeight = (int)Math.Round(3d * scaling);

        return pt.X >= islandRect.X && pt.X < islandRect.Right
            && pt.Y >= stripTop && pt.Y < stripTop + stripHeight;
    }

    /// <summary>隐藏态：光标在岛区内连续驻留 300ms → 进入等待态。</summary>
    private void UpdateDwell(bool overIsland)
    {
        if (_sm.Current != IslandVisualState.Hidden)
        {
            _dwellStarted = null;
            return;
        }

        if (!overIsland)
        {
            _dwellStarted = null;
            return;
        }

        _dwellStarted ??= DateTime.UtcNow;
        if ((DateTime.UtcNow - _dwellStarted.Value).TotalMilliseconds >= HoverDwellMs)
        {
            _dwellStarted = null;
            _sm.TryTransition(IslandVisualState.Waiting);
        }
    }

    public HudWindow(IPluginRegistry registry)
    {
        InitializeComponent();

        _registry = registry;
        _battery = registry.Resolve<BatteryPlugin>() ?? new BatteryPlugin();

        // 滚轮轮转集合：注册顺序即轮转顺序（电池在首位）。
        // 皮肤把 ParticipatesInWheelSwitch 覆写为 false 即可把自己排除出轮转。
        _skins = registry.Plugins.OfType<IIslandSkin>()
            .Where(s => s.ParticipatesInWheelSwitch)
            .ToList();
        if (!_skins.Contains(_battery))
            _skins.Insert(0, _battery);

        // --demo-music：初始切到「音乐」插件皮肤（外部 DLL 已由 PluginLoader 载入并注册）；
        // 宿主只按 IIslandSkin 契约认识它，不引用其具体类型。
        bool musicDemo = Array.Exists(Environment.GetCommandLineArgs(), a => a == "--demo-music");
        ActivateSkin(musicDemo ? FindSkinById("music") ?? _skins[0] : _skins[0]);
        _battery.RefreshLocalization();

        _sm.StateChanged += OnIslandStateChanged;

        PointerPressed += OnPointerPressed;
        PointerWheelChanged += OnPointerWheelChanged;

        _fpsEnabled = Array.Exists(Environment.GetCommandLineArgs(), a => a == "--show-fps");
        if (_fpsEnabled)
        {
            FpsText.IsVisible = true;
            StartFpsCounter();
        }
    }

    // ---------------- 岛皮肤轮转（鼠标滚轮切换） ----------------

    /// <summary>切换活动皮肤：换视觉树 / 窗口高度，缩放同步到全局缩放，并重挂展开按钮转发。</summary>
    private void ActivateSkin(IIslandSkin skin)
    {
        if (!ReferenceEquals(_skin, skin) && _skin is IIslandExpandToggle old)
            old.ExpandToggled -= OnExpandToggled;

        _skin = skin;

        if (skin is IIslandExpandToggle toggle)
            toggle.ExpandToggled += OnExpandToggled;

        IslandHost.Content = skin.View;
        ApplyWindowHeight(skin.WindowHeight);
        skin.ApplyScale(_settings.GlobalScale);
    }

    /// <summary>窗口高度随皮肤走（电池 160 / 音乐 220）。</summary>
    private void ApplyWindowHeight(double height)
    {
        Height = height;
        RootGrid.Height = height;
        IslandHost.Height = height;
    }

    /// <summary>
    /// 滚轮切换岛，按「翻页」语义：每一档滚动 = 翻一页（切一个皮肤），不做手势判定。
    /// 一次事件里若挤了多档（快速滚轮被系统合并）就翻同样多的页。
    /// 边界行为与是否启用由设置项 <see cref="AppSettings.WheelSwitch"/> 决定（循环 / 到边界即停 / 禁用）。
    /// 窗口默认整窗穿透，只有指针在岛上时宿主才临时取消穿透，故滚轮只在岛上生效。
    /// </summary>
    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (_skins.Count <= 1)
            return;

        if (_settings.WheelSwitch == WheelSwitchMode.Disabled)
            return;

        int pages = Math.Max(1, (int)Math.Round(Math.Abs(e.Delta.Y)));
        int step = e.Delta.Y > 0 ? -1 : 1;

        for (int i = 0; i < pages; i++)
        {
            int index = _skins.IndexOf(_skin);
            if (index < 0)
                index = 0;

            int next = index + step;
            if (next < 0 || next >= _skins.Count)
            {
                if (_settings.WheelSwitch == WheelSwitchMode.Clamp)
                    break; // 到边界即停

                next = (next + _skins.Count) % _skins.Count; // 循环
            }

            SwitchTo(_skins[next]);
        }

        e.Handled = true;
    }

    /// <summary>切到指定皮肤并重播等待态（滚轮切换）。</summary>
    private void SwitchTo(IIslandSkin skin) => SwitchTo(skin, expanded: false);

    /// <summary>
    /// 切换皮肤并按需以「展开态」展示（expanded = 走响应态而非等待态）。
    /// 切换期间抑制状态机回调，播放统一在这里驱动一次。
    /// </summary>
    private void SwitchTo(IIslandSkin skin, bool expanded)
    {
        _switching = true;
        try
        {
            ActivateSkin(skin);
            _sm.Force(expanded ? IslandVisualState.Response : IslandVisualState.Waiting);
        }
        finally
        {
            _switching = false;
        }

        Logger.Info(expanded ? $"Hud: 展开岛 → {skin.Id}" : $"Hud: 滚轮切换岛 → {skin.Id}");

        var ct = RefreshCts();

        if (expanded)
        {
            _skin.PrepareRevealStart();
            ShowPositioned();
            _ = _skin.PlayResponseAsync(ct);
            ScheduleExpandedCollapse(); // 展开态回落：由本入口显式排定，不再依赖指针轮询兜底
            return;
        }

        _ = EnterWaitingAsync(ct, IslandVisualState.Hidden);
    }

    /// <summary>贡献设置面板的插件（设置窗口「插件」页用；宿主只按契约认识它们）。</summary>
    public IReadOnlyList<IPluginSettingsPage> SettingsPages =>
        _registry.Plugins.OfType<IPluginSettingsPage>().ToList();

    // ---------------- IIslandHost（宿主服务，插件经 IPluginContext.GetService<IIslandHost>() 取用） ----------------

    public string CurrentSkinId => _skin.Id;

    public IReadOnlyList<string> WheelSkinIds => _skins.Select(s => s.Id).ToArray();

    public bool SwitchSkin(string id)
    {
        var target = _skins.FirstOrDefault(s => s.Id == id) ?? FindSkinById(id);
        if (target is null || ReferenceEquals(target, _skin))
            return false;

        SwitchTo(target);
        return true;
    }

    public bool ShowExpanded(string id)
    {
        var target = _skins.FirstOrDefault(s => s.Id == id) ?? FindSkinById(id);
        if (target is null)
            return false;

        // 已经是该皮肤的展开态：不重播动画
        if (ReferenceEquals(target, _skin) && _sm.Current == IslandVisualState.Response)
            return true;

        SwitchTo(target, expanded: true);
        return true;
    }

    /// <summary>岛内按键：右键打开自定义上下文菜单；其他按键（含左键）维持原有消失行为。</summary>
    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
        {
            OpenContextMenu();
            return;
        }

        _ = DismissAsync();
    }

    /// <summary>在灵动岛正下方弹出右键菜单（水平居中于岛，夹紧到目标显示器工作区）。</summary>
    private void OpenContextMenu()
    {
        _contextMenu?.Close();
        _contextMenu = null;

        var screen = ResolveScreen(_settings.MonitorIndex) ?? Screens.Primary;
        if (screen is null)
            return;

        var menu = new IslandContextMenuWindow(_settings, _registry);
        menu.SettingsClicked += () =>
        {
            var win = new SettingsWindow(_settings, this);
            win.Show();
        };
        menu.ExitClicked += () =>
        {
            // 与托盘菜单「退出」同一语义：走桌面生命周期，触发 App.OnDesktopExit 收尾
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.Shutdown();
        };
        menu.MenuClosed += OnContextMenuClosed;

        // 菜单打开期间暂停空闲计时（避免岛在菜单后面收缩/隐藏）
        _menuOpen = true;
        CancelIdle();

        _contextMenu = menu;
        menu.ShowBelowIsland(GetIslandBoundsPixels(), screen);
    }

    /// <summary>右键菜单关闭 → 恢复空闲计时。</summary>
    private void OnContextMenuClosed()
    {
        _menuOpen = false;
        ResumeIdleIfIdleState();
    }

    /// <summary>从设置更新 HUD 参数（缩放、动画微调、位置、显示器、置顶、超时）。</summary>
    public void ApplySettings(AppSettings settings)
    {
        _settings = settings;
        _animOptions = AnimationOptions.FromSettings(settings);

        // 窗口置顶
        Topmost = settings.WindowTopmost;

        // 全局缩放 + 本地化文案（可能语言变了）→ 皮肤
        _skin.ApplyScale(settings.GlobalScale);
        _battery.RefreshLocalization();

        // 超时变更：正在计时的状态按新值重排
        ResumeIdleIfIdleState();
    }

    // ---------------- 4 态生命周期（状态机驱动） ----------------

    /// <summary>状态变化 → 驱动皮肤 + 空闲计时。</summary>
    private void OnIslandStateChanged(IslandVisualState from, IslandVisualState to)
    {
        CancelIdle();

        // 记录皮肤与尺寸：便于区分「宿主状态」与「皮肤视图」不一致（如视图停在收缩态）
        var metrics = _skin.CurrentMetrics;
        Logger.Info($"Hud: 状态 {from} → {to}（皮肤 {_skin.Id}，{metrics.Width:F0}×{metrics.Height:F0}）");

        if (_switching)
            return; // 滚轮切换：状态归一与播放统一由 SwitchTo 驱动

        switch (to)
        {
            case IslandVisualState.Response:
                // 动画由触发入口（TriggerResponseCoreAsync）播放。
                // 展开态是否/多久回落到等待态由皮肤自报（IIslandSkin.ExpandedTimeoutSeconds，
                // 0 = 不回落）；音乐插件的「展开态超时」设置即通过它生效。
                ScheduleExpandedCollapse();
                break;

            case IslandVisualState.Waiting:
                {
                    var ct = RefreshCts();
                    _ = EnterWaitingAsync(ct, from); // 揭示完成后再排 T1（见 EnterWaitingAsync）
                    break;
                }

            case IslandVisualState.Contracted:
                {
                    var ct = RefreshCts();
                    _ = _skin.PlayContractAsync(ct);
                    ScheduleIdle(
                        TimeSpan.FromSeconds(_settings.ContractedTimeoutSeconds),
                        IslandVisualState.Hidden);
                    break;
                }

            case IslandVisualState.Hidden:
                _ = PlayDismissAndHideAsync();
                break;
        }
    }

    /// <summary>进入等待态：（宿主内容皮肤）刷新电量内容 →（自隐藏时）布置揭示起点并显示窗口 → 等待态动画。
    /// T1 在动画完成后才排定，避免计时器切断揭示动画；与唤醒方式无关，所有皮肤一致。</summary>
    private async Task EnterWaitingAsync(CancellationToken ct, IslandVisualState from)
    {
        // 宿主内容皮肤（电池）：抓取一帧并绑定；自给数据皮肤（音乐 SMTC）跳过
        if (_skin.UsesHostContent)
        {
            var snap = await _battery.FetchSnapshotAsync();
            if (ct.IsCancellationRequested)
                return; // 期间被响应预占

            _skin.BindContent(_battery.CreateDescriptor(snap, powerSaver: false, IslandPlayKind.Simple));
        }

        // 仅隐藏 → 等待需要布置揭示起点（窗口首帧即起点状态，无整只胶囊闪现）+ 显示窗口；
        // 响应 → 等待 / 收缩 → 等待时窗口已可见，直接交给 PlayWaitingAsync（保持/展开）。
        if (from == IslandVisualState.Hidden)
        {
            _skin.PrepareRevealStart();
            ShowPositioned();
        }

        await _skin.PlayWaitingAsync(ct);

        if (ct.IsCancellationRequested)
        {
            // 诊断：揭示动画被后续切换打断（视图可能停在中间态）
            Logger.Info($"Hud: 等待态播放被取消（{_skin.Id}）");
            return; // 揭示被预占/取消，不排 T1
        }

        var settled = _skin.CurrentMetrics;
        Logger.Info($"Hud: 等待态就位（{_skin.Id}，{settled.Width:F0}×{settled.Height:F0}）");

        // 揭示/展开/保持完成 → 排 T1（等待态生命周期的超时，与唤醒方式无关）
        ScheduleIdle(
            TimeSpan.FromSeconds(_settings.WaitingTimeoutSeconds),
            IslandVisualState.Contracted);
    }

    /// <summary>退场到隐藏：中断动画 → 淡出 → 隐藏窗口。</summary>
    private async Task PlayDismissAndHideAsync()
    {
        _cts?.Cancel();
        await _skin.PlayDismissAsync(CancellationToken.None);
        Hide();
    }

    /// <summary>取消并重建 _cts，返回新令牌（响应/等待/收缩动画共用，互斥）。</summary>
    private CancellationToken RefreshCts()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        return _cts.Token;
    }

    /// <summary>展开态生命周期下限：每个状态都必须有超时，皮肤声明再小也按这个值执行。</summary>
    private const double MinExpandedTimeoutSeconds = 3d;

    /// <summary>按当前皮肤声明的展开态超时排定「回落到等待态」（低于下限则按下限执行）。</summary>
    private void ScheduleExpandedCollapse()
    {
        double seconds = Math.Max(MinExpandedTimeoutSeconds, _skin.ExpandedTimeoutSeconds);

        Logger.Info($"Hud: 展开态将在 {seconds:F0}s 无交互后回落到等待态（皮肤 {_skin.Id}）");
        ScheduleIdle(TimeSpan.FromSeconds(seconds), IslandVisualState.Waiting);
    }

    /// <summary>
    /// 空闲计时（T1/T2 复用单计时器 + 代际令牌防过期回调）。
    /// 到期 → TryTransition(_idleTarget)；非法转换由状态机拒绝并记录。
    /// </summary>
    private void ScheduleIdle(TimeSpan timeout, IslandVisualState expiryTarget)
    {
        CancelIdle();

        _idleTarget = expiryTarget;
        int gen = ++_idleGen;

        var timer = new DispatcherTimer { Interval = timeout };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (gen != _idleGen)
                return; // 已取消 / 已重排的过期回调

            Logger.Info($"Hud: 空闲计时到期 → {_idleTarget}（皮肤 {_skin.Id}）");
            _sm.TryTransition(_idleTarget);
        };
        _idleTimer = timer;
        timer.Start();
    }

    private void CancelIdle()
    {
        _idleGen++;
        _idleTimer?.Stop();
        _idleTimer = null;
    }

    /// <summary>
    /// 指针离开岛后按当前状态重排空闲计时（已有计时在跑则不重复排）。
    /// 每个状态都有自己的生命周期超时，与该状态被何种方式唤醒无关：
    /// 展开态 → 等待态（皮肤自报时长）、等待态 → 收缩态、收缩态 → 隐藏态。
    /// </summary>
    private void ResumeIdleIfIdleState()
    {
        if (_idleTimer is { IsEnabled: true })
            return;

        if (_sm.Current == IslandVisualState.Response)
        {
            ScheduleExpandedCollapse();
            return;
        }

        if (_sm.Current == IslandVisualState.Waiting)
        {
            ScheduleIdle(
                TimeSpan.FromSeconds(_settings.WaitingTimeoutSeconds),
                IslandVisualState.Contracted);
        }
        else if (_sm.Current == IslandVisualState.Contracted)
        {
            ScheduleIdle(
                TimeSpan.FromSeconds(_settings.ContractedTimeoutSeconds),
                IslandVisualState.Hidden);
        }
    }

    /// <summary>
    /// 响应入口（公共 API 委托）：任何状态 → Response（预占一切），
    /// 动画结束于状态 C 后进入 Waiting。--debug-ring 保持静态并隐藏。
    /// </summary>
    private async Task TriggerResponseCoreAsync(
        BatterySnapshot? battery,
        IslandPlayKind playKind,
        bool powerSaver,
        AnimationOptions? options)
    {
        // 电池事件（插拔电 / 省电切换）：若当前停在自给数据皮肤（音乐）上，先切回宿主内容皮肤
        if (!_skin.UsesHostContent)
            ActivateSkin(_skins.FirstOrDefault(s => s.UsesHostContent) ?? _battery);

        if (_sm.Current != IslandVisualState.Response)
        {
            if (!_sm.TryTransition(IslandVisualState.Response))
                return;
        }

        _battery.SetAnimationOptions(options ?? _animOptions);
        _battery.BindContent(_battery.CreateDescriptor(battery, powerSaver, playKind));

        var ct = RefreshCts();

        bool debugStatic = Array.Exists(Environment.GetCommandLineArgs(), a => a == "--debug-ring");

        ShowPositioned();

        if (debugStatic)
        {
            _battery.ShowStatic();
            try { await Task.Delay(1500, ct); }
            catch (OperationCanceledException) { return; }
            if (!ct.IsCancellationRequested)
                _sm.Force(IslandVisualState.Hidden);
            return;
        }

        await _skin.PlayResponseAsync(ct);

        if (ct.IsCancellationRequested)
            return;

        // 响应结束 → 等待态（触发 T1）
        _sm.TryTransition(IslandVisualState.Waiting);
    }

    public async Task ShowSimpleAsync(BatterySnapshot? battery, AnimationOptions? options = null) =>
        await TriggerResponseCoreAsync(battery, IslandPlayKind.Simple, powerSaver: false, options);

    public async Task ShowAndPlayAsync(
        BatterySnapshot? battery,
        bool acOnline,
        HudPlayMode mode = HudPlayMode.Charge,
        AnimationOptions? options = null) =>
        await TriggerResponseCoreAsync(battery, IslandPlayKind.Full, mode == HudPlayMode.PowerSaver, options);

    /// <summary>灵动岛当前是否可见（托盘左键开关用）。</summary>
    public bool IsIslandVisible => IsVisible;

    /// <summary>隐藏灵动岛（任意状态强制隐藏，含响应中；右键菜单一并收起）。</summary>
    public void HideIsland()
    {
        _contextMenu?.Close();
        _contextMenu = null;

        _sm.Force(IslandVisualState.Hidden);
    }

    /// <summary>等待态入口（托盘左键等）：隐藏 / 收缩 → 等待；响应中忽略（完成事件进入等待）。</summary>
    public async Task ShowWaitingAsync()
    {
        if (_sm.Current is IslandVisualState.Hidden or IslandVisualState.Contracted)
            _sm.TryTransition(IslandVisualState.Waiting);

        await Task.CompletedTask;
    }

    /// <summary>岛内左键：维持原有消失语义（强制隐藏）。</summary>
    private async Task DismissAsync()
    {
        _contextMenu?.Close();
        _contextMenu = null;

        _sm.Force(IslandVisualState.Hidden);
        await Task.CompletedTask;
    }

    /// <summary>启动音乐模式（--demo-music）：进入等待态；数据由音乐插件自行轮询 SMTC。</summary>
    public Task StartMusicDemoAsync()
    {
        if (_sm.Current != IslandVisualState.Waiting)
            _sm.Force(IslandVisualState.Waiting);
        return Task.CompletedTask;
    }

    /// <summary>按插件 Id 找皮肤（外部插件不在宿主编译期类型系统里）。</summary>
    private IIslandSkin? FindSkinById(string id)
    {
        foreach (var plugin in _registry.Plugins)
        {
            if (plugin.Id == id && plugin is IIslandSkin skin)
                return skin;
        }
        return null;
    }

    /// <summary>岛内「展开 / 收起」按钮（IIslandExpandToggle）→ 等待态 ↔ 展开态（复用 Response 状态）。</summary>
    private void OnExpandToggled()
    {
        if (_sm.Current == IslandVisualState.Waiting)
        {
            if (_sm.TryTransition(IslandVisualState.Response))
            {
                var ct = RefreshCts();
                _ = PlayMusicResponseAsync(ct);
            }
        }
        else if (_sm.Current == IslandVisualState.Response)
        {
            _sm.TryTransition(IslandVisualState.Waiting);
        }
    }

    private async Task PlayMusicResponseAsync(CancellationToken ct)
    {
        ShowPositioned();
        await _skin.PlayResponseAsync(ct);
    }

    // ---------------- FPS 计数器 ----------------

    private void StartFpsCounter()
    {
        DispatcherTimer.Run(() =>
        {
            if (!IsVisible)
            {
                _fpsFrameCount = 0;
                _fpsLastMeasure = DateTime.UtcNow;
                return true;
            }

            _fpsFrameCount++;

            var now = DateTime.UtcNow;
            var elapsed = (now - _fpsLastMeasure).TotalSeconds;
            if (elapsed >= 1.0)
            {
                double fps = _fpsFrameCount / elapsed;
                FpsText.Text = $"{fps:F0} FPS";
                _fpsFrameCount = 0;
                _fpsLastMeasure = now;
            }

            return true;
        }, TimeSpan.FromMilliseconds(200));
    }

    // ---------------- 定位（多显示器 + 位置选择） ----------------

    private void PositionTopCenter()
    {
        var screen = ResolveScreen(_settings.MonitorIndex);
        if (screen is null) return;

        var area = screen.WorkingArea;

        // screen.Scaling 来自显示器 DPI 枚举，比窗口的 RenderScaling 可靠（后者首帧前可能未更新）
        double scaling = screen.Scaling > 0 ? screen.Scaling : 1d;
        int pixelWidth = (int)Math.Round(Width * scaling);

        int x = _settings.HudPosition switch
        {
            HudPosition.TopLeft => area.X + 10,
            HudPosition.TopRight => area.X + area.Width - pixelWidth - 10,
            _ => area.X + (area.Width - pixelWidth) / 2, // TopCenter
        };

        // 灵动岛整体上移：默认 +4 → -48（上移 52px），使其更靠近屏幕顶部
        Position = new PixelPoint(x, area.Y - 48);
    }

    /// <summary>
    /// 解析目标显示器：-1 = 主显示器（默认），0..N-1 = 显示器列表索引，越界退回主显示器。
    /// </summary>
    private Avalonia.Platform.Screen? ResolveScreen(int monitorIndex)
    {
        var screens = Screens.All;
        var primary = Screens.Primary;

        if (monitorIndex < 0)
            return primary ?? screens.FirstOrDefault();

        if (monitorIndex < screens.Count)
            return screens[monitorIndex];

        return primary ?? screens.FirstOrDefault();
    }

    private void ShowPositioned()
    {
        PositionTopCenter();

        if (!IsVisible)
            Show();

        PositionTopCenter();
        Dispatcher.UIThread.Post(() =>
        {
            PositionTopCenter();
        }, DispatcherPriority.Loaded);
    }
}
