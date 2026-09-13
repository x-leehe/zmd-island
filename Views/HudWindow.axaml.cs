using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using EndfieldCharge.Animations;
using EndfieldCharge.Contracts;
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
public partial class HudWindow : Window
{
    private CancellationTokenSource? _cts;
    private AppSettings _settings = new();
    private AnimationOptions _animOptions = AnimationOptions.Default;
    private int _fpsFrameCount;
    private DateTime _fpsLastMeasure = DateTime.UtcNow;
    private bool _fpsEnabled;
    private IslandContextMenuWindow? _contextMenu;

    /// <summary>电池元插件：岛皮肤 + 内容映射（从注册表解析，保证与注册实例同一份）。</summary>
    private readonly BatteryPlugin _battery;

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
        double gs = Math.Clamp(_settings.GlobalScale, 0.1, 2d);

        double w = _battery.PillWidthDips * gs * scaling;
        double h = 60d * gs * scaling;
        double cx = Position.X + Width / 2d * scaling;
        double cy = Position.Y + Height / 2d * scaling;

        return new PixelRect(
            (int)Math.Round(cx - w / 2d),
            (int)Math.Round(cy - h / 2d),
            (int)Math.Round(w),
            (int)Math.Round(h));
    }

    /// <summary>岛悬停唤醒区（物理像素）：完整胶囊宽度 560×GlobalScale——不随收缩态收窄，
    /// 悬停在岛完整区域内即保持等待态。</summary>
    private PixelRect GetIslandHoverBoundsPixels()
    {
        var screen = ResolveScreen(_settings.MonitorIndex);
        double scaling = screen is { Scaling: > 0 } ? screen.Scaling : 1d;
        double gs = Math.Clamp(_settings.GlobalScale, 0.1, 2d);

        double w = 560d * gs * scaling;
        double h = 60d * gs * scaling;
        double cx = Position.X + Width / 2d * scaling;
        double cy = Position.Y + Height / 2d * scaling;

        return new PixelRect(
            (int)Math.Round(cx - w / 2d),
            (int)Math.Round(cy - h / 2d),
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
        IslandHost.Content = _battery.View;
        _battery.RefreshLocalization();

        _sm.StateChanged += OnIslandStateChanged;

        PointerPressed += OnPointerPressed;

        _fpsEnabled = Array.Exists(Environment.GetCommandLineArgs(), a => a == "--show-fps");
        if (_fpsEnabled)
        {
            FpsText.IsVisible = true;
            StartFpsCounter();
        }
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
        _battery.ApplyScale(settings.GlobalScale);
        _battery.RefreshLocalization();

        // 超时变更：正在计时的状态按新值重排
        ResumeIdleIfIdleState();
    }

    // ---------------- 4 态生命周期（状态机驱动） ----------------

    /// <summary>状态变化 → 驱动皮肤 + 空闲计时。</summary>
    private void OnIslandStateChanged(IslandVisualState from, IslandVisualState to)
    {
        CancelIdle();

        switch (to)
        {
            case IslandVisualState.Response:
                // 动画由触发入口（TriggerResponseCoreAsync）播放
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
                    _ = _battery.PlayContractAsync(ct);
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

    /// <summary>进入等待态：刷新电量内容 →（自隐藏时）布置揭示起点并显示窗口 → 等待态动画。
    /// T1 在动画完成后才排定，避免计时器切断揭示动画。</summary>
    private async Task EnterWaitingAsync(CancellationToken ct, IslandVisualState from)
    {
        var snap = await _battery.FetchSnapshotAsync();
        if (ct.IsCancellationRequested)
            return; // 期间被响应预占

        _battery.BindContent(_battery.CreateDescriptor(snap, powerSaver: false, IslandPlayKind.Simple));

        // 仅隐藏 → 等待需要布置揭示起点（窗口首帧即起点状态，无整只胶囊闪现）+ 显示窗口；
        // 响应 → 等待 / 收缩 → 等待时窗口已可见，直接交给 PlayWaitingAsync（保持/展开）。
        if (from == IslandVisualState.Hidden)
        {
            _battery.PrepareRevealStart();
            ShowPositioned();
        }

        await _battery.PlayWaitingAsync(ct);
        if (ct.IsCancellationRequested)
            return; // 揭示被预占/取消，不排 T1

        // 揭示/展开/保持完成 → 排 T1（等待态超时）
        ScheduleIdle(
            TimeSpan.FromSeconds(_settings.WaitingTimeoutSeconds),
            IslandVisualState.Contracted);
    }

    /// <summary>退场到隐藏：中断动画 → 淡出 → 隐藏窗口。</summary>
    private async Task PlayDismissAndHideAsync()
    {
        _cts?.Cancel();
        await _battery.PlayDismissAsync(CancellationToken.None);
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

    /// <summary>若当前处于等待/收缩态则（重新）按设置排空闲计时；已有计时在跑则不重复排。</summary>
    private void ResumeIdleIfIdleState()
    {
        if (_idleTimer is { IsEnabled: true })
            return;

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

        await _battery.PlayResponseAsync(ct);

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
