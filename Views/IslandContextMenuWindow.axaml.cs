using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using EndfieldCharge.Contracts;
using EndfieldCharge.Host.Menu;
using EndfieldCharge.Host.Plugins;
using EndfieldCharge.Settings;
using Path = Avalonia.Controls.Shapes.Path;

namespace EndfieldCharge.Views;

/// <summary>
/// 灵动岛右键自定义上下文菜单（illogical-impulse 风格，插件驱动）。
///
/// 与 TrayMenuWindow 的关键差异：**绝不在 Deactivated / LostFocus 时关闭**。
/// 用户环境里第三方托盘程序会在右键瞬间抢焦点，普通菜单会「开了立刻关」。
/// 因此本窗口：
///   1. 通过 WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW 成为「永不激活」窗口——不会获得焦点，
///      也不会触发 Deactivated；
///   2. 用全局低级鼠标钩子（WH_MOUSE_LL）兜底关闭：任意鼠标按下落在菜单表面之外即关闭。
/// 钩子安装在 Avalonia UI 线程上（其消息循环负责投递 WH_MOUSE_LL 回调）。
///
/// 条目 = PluginMenuComposer.Compose(...) 动态生成（无硬编码条目行），
/// 窗口 / 表面尺寸、子菜单位置与钩子命中区都按生成行数计算。
/// </summary>
public partial class IslandContextMenuWindow : Window
{
    public event Action? SettingsClicked;

    /// <summary>「退出」条目被点击：宿主据此关闭应用（与托盘菜单的退出项同一语义）。</summary>
    public event Action? ExitClicked;

    /// <summary>菜单真正关闭（含外部点击 / 退场动画后），宿主借此恢复岛的空闲计时。</summary>
    public event Action? MenuClosed;

    // ---- 布局常量（DIP）----
    private const double SurfaceLeft = 8d;
    private const double SurfaceTop = 12d;      // 窗口上缘透明边距（滑入动画起点）
    private const double SurfaceWidth = 220d;
    private const double SubmenuWidth = 200d;
    private const double SubmenuGap = 4d;
    private const double RowHeight = 32d;
    private const double StackSpacing = 2d;
    private const double SurfacePadding = 6d;
    private const double SeparatorTotal = 7d;   // 1px 线 + 上下 3px 边距
    private const double BottomMargin = 4d;
    private const double SlideOffset = 12d;

    private const string HostTopmostId = "host.topmost";
    private const string HostPluginsId = "host.plugins";

    private static readonly TimeSpan OpenDuration = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan CloseDuration = TimeSpan.FromMilliseconds(150);

    private static readonly Color HoverColor = Color.Parse("#363636");
    private static readonly Color FgColor = Color.Parse("#E8E8E8");
    private static readonly Color DisabledFgColor = Color.Parse("#5A5A5C");
    private static readonly Color SeparatorColor = Color.Parse("#2A2A2C");
    private static readonly Color AccentColor = Color.Parse("#C6CA4C");
    private static readonly Color ArrowColor = Color.Parse("#8A8A8A");

    private AppSettings _settings;
    private double _scaling = 1d;
    private PixelRect _menuRect;        // 主表面命中区（设备像素）
    private PixelRect? _submenuRect;    // 子菜单命中区（展开时）
    private bool _closing;
    private CancellationTokenSource? _animCts;
    private CancellationTokenSource? _submenuCloseCts;

    // ---- 动态布局（BuildMenu 计算）----
    private IReadOnlyList<MenuContribution> _contributions = Array.Empty<MenuContribution>();
    private MenuContribution? _pluginsItem;   // 「插件 ▸」宿主固定项
    private double _surfaceH;
    private double _subLeft;
    private double _subTop;
    private double _subH;
    private Path? _topmostCheck;

    public IslandContextMenuWindow(AppSettings settings, IPluginRegistry registry)
    {
        _settings = settings;
        _mouseProc = OnLowLevelMouse; // 委托必须先固定到字段，防止 GC 回收钩子回调
        InitializeComponent();

        BuildMenu(registry);
    }

    // ---------------- 动态构建（插件驱动） ----------------

    /// <summary>
    /// 合成条目：插件内容区 → 分隔线 → 宿主固定项（窗口置顶 / 插件▸ / 设置），
    /// 然后生成行、计算表面与窗口尺寸、子菜单位置。
    /// </summary>
    private void BuildMenu(IPluginRegistry registry)
    {
        var pluginChildren = PluginMenuComposer.BuildPluginChildren(MenuTarget.Island, registry);

        var topmost = new MenuContribution
        {
            Id = HostTopmostId,
            Header = Localization.Topmost,
            Target = MenuTarget.Island,
            Section = MenuSection.HostFixed,
            IsChecked = _settings.WindowTopmost,
            Command = OnToggleTopmost,
        };
        var plugins = new MenuContribution
        {
            Id = HostPluginsId,
            Header = Localization.Plugins,
            Target = MenuTarget.Island,
            Section = MenuSection.HostFixed,
            Children = pluginChildren,
        };
        var settings = new MenuContribution
        {
            Id = "host.settings",
            Header = Localization.Settings,
            Target = MenuTarget.Island,
            Section = MenuSection.HostFixed,
            Command = () =>
            {
                RequestClose();
                SettingsClicked?.Invoke();
            },
        };
        var exit = new MenuContribution
        {
            Id = "host.exit",
            Header = Localization.Exit,
            Target = MenuTarget.Island,
            Section = MenuSection.HostFixed,
            Command = () =>
            {
                RequestClose();
                ExitClicked?.Invoke();
            },
        };

        var hostFixed = new[] { topmost, plugins, settings, exit };
        _pluginsItem = plugins;
        _contributions = PluginMenuComposer.Compose(MenuTarget.Island, registry, hostFixed);

        var hover = new SolidColorBrush(HoverColor);
        var fg = new SolidColorBrush(FgColor);
        var sep = new SolidColorBrush(SeparatorColor);

        MenuStack.Children.Clear();
        SubStack.Children.Clear();

        // ---- 主表面行 ----
        double y = SurfacePadding;
        for (int i = 0; i < _contributions.Count; i++)
        {
            var item = _contributions[i];

            if (item.IsSeparator)
            {
                MenuStack.Children.Add(new Border
                {
                    Height = 1,
                    Background = sep,
                    Margin = new Thickness(12, 3),
                });
                y += SeparatorTotal;
            }
            else
            {
                var row = BuildRow(item, hover, fg);
                MenuStack.Children.Add(row);
                if (ReferenceEquals(item, _pluginsItem))
                    _pluginsRowTop = y;

                // 「插件 ▸」悬停展开子菜单
                if (ReferenceEquals(item, _pluginsItem) && item.Children is { Count: > 0 })
                {
                    row.PointerEntered += (_, _) =>
                    {
                        CancelSubmenuClose();
                        OpenSubmenu();
                    };
                    row.PointerExited += (_, _) => ScheduleSubmenuClose();
                }

                y += RowHeight;
            }

            if (i < _contributions.Count - 1)
                y += StackSpacing;
        }
        _surfaceH = y + SurfacePadding;
        MainSurface.Height = _surfaceH;

        // ---- 子菜单行（插件 ▸ children）----
        var children = _pluginsItem?.Children;
        if (children is { Count: > 0 })
        {
            double subY = SurfacePadding;
            for (int i = 0; i < children.Count; i++)
            {
                SubStack.Children.Add(BuildRow(children[i], hover, fg));
                subY += RowHeight;
                if (i < children.Count - 1)
                    subY += StackSpacing;
            }
            _subH = subY + SurfacePadding;
            SubSurface.Height = _subH;
        }
        else
        {
            _subH = 0d;
        }

        // ---- 子菜单位置 + 窗口尺寸（随行数计算）----
        _subLeft = SurfaceLeft + SurfaceWidth + SubmenuGap;
        _subTop = SurfaceTop + _pluginsRowTop;

        Canvas.SetLeft(SubSurface, _subLeft);
        Canvas.SetTop(SubSurface, _subTop);
        SubSurface.Width = SubmenuWidth;

        Width = _subLeft + SubmenuWidth + SurfaceLeft;
        Height = Math.Max(_surfaceH, _subTop + _subH) + SurfaceTop + BottomMargin;

        // 悬停穿过两表面之间 4px 间隙时保持展开
        if (children is { Count: > 0 })
        {
            SubSurface.PointerEntered += (_, _) => CancelSubmenuClose();
            SubSurface.PointerExited += (_, _) => ScheduleSubmenuClose();
        }
    }

    private double _pluginsRowTop;

    /// <summary>按贡献生成一行：文案 +（子菜单箭头 | 对勾），禁用行变暗且无交互。</summary>
    private Border BuildRow(MenuContribution item, IBrush hover, IBrush fg)
    {
        var row = new Border
        {
            Height = RowHeight,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 0),
            Cursor = new Cursor(StandardCursorType.Hand),
            Background = Brushes.Transparent,
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        row.Child = grid;

        var text = new TextBlock
        {
            Text = item.Header,
            FontSize = 13,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Foreground = item.IsEnabled ? fg : new SolidColorBrush(DisabledFgColor),
        };
        grid.Children.Add(text);

        if (item.Children is { Count: > 0 })
        {
            // 子菜单箭头
            var arrow = new Path
            {
                Width = 8,
                Height = 12,
                Data = Geometry.Parse("M0,0 L6,6 L0,12"),
                Stroke = new SolidColorBrush(ArrowColor),
                StrokeThickness = 1.5,
                StrokeLineCap = PenLineCap.Round,
                StrokeJoin = PenLineJoin.Round,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            };
            Grid.SetColumn(arrow, 1);
            grid.Children.Add(arrow);
        }
        else if (item.IsChecked || item.Id == HostTopmostId)
        {
            // 对勾（可勾选行：勾选态显示；窗口置顶行始终保留对勾槽位供切换后刷新）
            var check = new Path
            {
                Width = 12,
                Height = 12,
                Data = Geometry.Parse("M0,6 L4,10 L11,2"),
                Stroke = new SolidColorBrush(AccentColor),
                StrokeThickness = 1.8,
                StrokeLineCap = PenLineCap.Round,
                StrokeJoin = PenLineJoin.Round,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                IsVisible = item.IsChecked,
            };
            Grid.SetColumn(check, 1);
            grid.Children.Add(check);

            if (item.Id == HostTopmostId)
                _topmostCheck = check;
        }

        if (item.IsEnabled)
        {
            row.PointerEntered += (_, _) => row.Background = hover;
            row.PointerExited += (_, _) => row.Background = Brushes.Transparent;
            row.PointerPressed += (_, _) => item.Command?.Invoke();
        }

        return row;
    }

    /// <summary>「窗口置顶」切换：翻转设置 → 持久化 → 通知 App（HUD.ApplySettings 生效）。</summary>
    private void OnToggleTopmost()
    {
        _settings = _settings with { WindowTopmost = !_settings.WindowTopmost };

        SettingsManager.Save(_settings);
        if (Application.Current is App app)
            app.OnSettingsChanged(_settings);

        if (_topmostCheck is not null)
            _topmostCheck.IsVisible = _settings.WindowTopmost;
    }

    // ---------------- 子菜单（悬停展开 / 移出延迟关闭） ----------------

    private void OpenSubmenu()
    {
        if (SubSurface.IsVisible || _subH <= 0d)
            return;

        SubSurface.IsVisible = true;
        _submenuRect = ComputeSubmenuScreenRect();

        // 从父项右缘轻微滑出 + 淡入
        SubSurface.RenderTransform = new TranslateTransform(-8d, 0d);
        SubSurface.Opacity = 0d;
        var anim = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(120),
            FillMode = FillMode.Forward,
            Easing = new QuadraticEaseOut(),
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0d),
                    Setters =
                    {
                        new Setter(TranslateTransform.XProperty, -8d),
                        new Setter(OpacityProperty, 0d),
                    },
                },
                new KeyFrame
                {
                    Cue = new Cue(1d),
                    Setters =
                    {
                        new Setter(TranslateTransform.XProperty, 0d),
                        new Setter(OpacityProperty, 1d),
                    },
                },
            },
        };
        _ = anim.RunAsync(SubSurface);
    }

    private void CloseSubmenu()
    {
        if (!SubSurface.IsVisible)
            return;

        SubSurface.IsVisible = false;
        SubSurface.Opacity = 1d;
        _submenuRect = null;
    }

    private void ScheduleSubmenuClose()
    {
        CancelSubmenuClose();
        _submenuCloseCts = new CancellationTokenSource();
        _ = CloseSubmenuDelayedAsync(_submenuCloseCts.Token);
    }

    private async Task CloseSubmenuDelayedAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(160, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        CloseSubmenu();
    }

    private void CancelSubmenuClose()
    {
        _submenuCloseCts?.Cancel();
        _submenuCloseCts?.Dispose();
        _submenuCloseCts = null;
    }

    // ---------------- Win32：永不激活 + 全局鼠标钩子 ----------------

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    private const int WH_MOUSE_LL = 14;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_MBUTTONDOWN = 0x0207;
    private const int WM_XBUTTONDOWN = 0x020B;

    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    /// <summary>钩子委托存字段防止被 GC（SetWindowsHookEx 只存了函数指针）。</summary>
    private readonly LowLevelMouseProc _mouseProc;
    private IntPtr _mouseHook;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out Win32Point lpPoint);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [StructLayout(LayoutKind.Sequential)]
    private struct Win32Point
    {
        public int X;
        public int Y;
    }

    /// <summary>窗口句柄可用后打上「不激活 + 工具窗口」扩展样式（OnOpened 时 HWND 已创建）。</summary>
    private void ApplyNonActivatingStyle()
    {
        var handle = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (handle == IntPtr.Zero)
            return;

        var exStyle = GetWindowLong(handle, GWL_EXSTYLE);
        SetWindowLong(handle, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
    }

    /// <summary>在 Avalonia UI 线程安装 WH_MOUSE_LL（UI 消息循环负责投递回调）。</summary>
    private void InstallMouseHook()
    {
        if (_mouseHook != IntPtr.Zero)
            return;

        _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, GetModuleHandle(null), 0);
        if (_mouseHook == IntPtr.Zero)
            Services.Logger.Error("IslandContextMenu: SetWindowsHookEx(WH_MOUSE_LL) 失败");
    }

    private void UninstallMouseHook()
    {
        if (_mouseHook == IntPtr.Zero)
            return;

        UnhookWindowsHookEx(_mouseHook);
        _mouseHook = IntPtr.Zero;
    }

    /// <summary>
    /// 钩子回调：必须在 UI 线程消息泵中执行、必须最小化开销、
    /// 必须始终调用 CallNextHookEx、绝不能抛出异常。
    /// </summary>
    private IntPtr OnLowLevelMouse(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode >= 0 && IsMouseDownMessage(wParam))
            {
                if (GetCursorPos(out var pt))
                {
                    bool inside = RectContains(_menuRect, pt.X, pt.Y)
                        || (_submenuRect is { } sub && RectContains(sub, pt.X, pt.Y));
                    if (!inside)
                        Dispatcher.UIThread.Post(RequestClose);
                }
            }
        }
        catch
        {
            // 钩子回调绝不抛出异常（抛了会崩进程）
        }

        return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private static bool IsMouseDownMessage(IntPtr wParam) =>
        wParam.ToInt32() is WM_LBUTTONDOWN or WM_RBUTTONDOWN or WM_MBUTTONDOWN or WM_XBUTTONDOWN;

    private static bool RectContains(PixelRect r, int x, int y) =>
        x >= r.X && x < r.Right && y >= r.Y && y < r.Bottom;

    // ---------------- 显示 / 关闭 / 动画 ----------------

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        ApplyNonActivatingStyle();
        InstallMouseHook();

        _animCts = new CancellationTokenSource();
        _ = BuildOpenAnimation().RunAsync(ContentRoot, _animCts.Token);
    }

    protected override void OnClosed(EventArgs e)
    {
        UninstallMouseHook();
        _animCts?.Cancel();
        CancelSubmenuClose();
        MenuClosed?.Invoke();
        base.OnClosed(e);
    }

    /// <summary>在灵动岛正下方显示菜单（水平居中于岛，夹紧到目标显示器工作区）。</summary>
    public void ShowBelowIsland(PixelRect islandRect, Avalonia.Platform.Screen screen)
    {
        double scaling = screen.Scaling > 0 ? screen.Scaling : 1d;
        var area = screen.WorkingArea;
        _scaling = scaling;

        int winW = (int)Math.Round(Width * scaling);
        int winH = (int)Math.Round(Height * scaling);
        int topMarginPx = (int)Math.Round(SurfaceTop * scaling);

        // 视觉上表面距岛底缘 8px；窗口上缘扣除 12px 滑入透明边距
        int x = islandRect.X + (islandRect.Width - winW) / 2;
        int y = islandRect.Bottom + 8 - topMarginPx;

        x = Math.Clamp(x, area.X, Math.Max(area.X, area.Right - winW));
        y = Math.Clamp(y, area.Y - topMarginPx, Math.Max(area.Y - topMarginPx, area.Bottom - winH));

        Position = new PixelPoint(x, y);

        // 命中区只认可见表面（设备像素，±2px 容差），按实际表面尺寸计算；子菜单初始收起
        _menuRect = new PixelRect(
            x + (int)Math.Round(SurfaceLeft * scaling),
            y + (int)Math.Round(SurfaceTop * scaling),
            (int)Math.Round(SurfaceWidth * scaling),
            (int)Math.Round(_surfaceH * scaling));
        _menuRect = InflateRect(_menuRect, 2);
        _submenuRect = null;

        Services.Logger.Info($"IslandContextMenu: island={islandRect} wa={area} pos=({x},{y}) rows={_contributions.Count}");
        Show();
    }

    private PixelRect ComputeSubmenuScreenRect()
    {
        double s = _scaling;
        var rect = new PixelRect(
            Position.X + (int)Math.Round(_subLeft * s),
            Position.Y + (int)Math.Round(_subTop * s),
            (int)Math.Round(SubmenuWidth * s),
            (int)Math.Round(_subH * s));
        return InflateRect(rect, 2);
    }

    private static PixelRect InflateRect(PixelRect r, int pad) =>
        new(r.X - pad, r.Y - pad, r.Width + 2 * pad, r.Height + 2 * pad);

    /// <summary>关闭入口（外部点击 / 条目点击）。先摘钩子，再播 150ms 退场动画后 Close。</summary>
    private void RequestClose()
    {
        if (_closing)
            return;
        _closing = true;

        UninstallMouseHook();

        _animCts?.Cancel();
        _animCts = new CancellationTokenSource();
        _ = CloseAnimatedAsync(_animCts.Token);
    }

    private async Task CloseAnimatedAsync(CancellationToken token)
    {
        try
        {
            await BuildCloseAnimation().RunAsync(ContentRoot, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (IsVisible)
            Close();
    }

    /// <summary>开场：从锚点边缘（上）滑出 + 淡入，200ms KeySpline(0,1,1,1)。</summary>
    private Animation BuildOpenAnimation() => new()
    {
        Duration = OpenDuration,
        FillMode = FillMode.Forward,
        Children =
        {
            new KeyFrame
            {
                Cue = new Cue(0d),
                Setters =
                {
                    new Setter(TranslateTransform.YProperty, -SlideOffset),
                    new Setter(OpacityProperty, 0d),
                },
            },
            new KeyFrame
            {
                Cue = new Cue(1d),
                KeySpline = new KeySpline(0d, 1d, 1d, 1d),
                Setters =
                {
                    new Setter(TranslateTransform.YProperty, 0d),
                    new Setter(OpacityProperty, 1d),
                },
            },
        },
    };

    /// <summary>退场：缩回锚点边缘 + 淡出，150ms KeySpline(1,0,1,1)。</summary>
    private Animation BuildCloseAnimation() => new()
    {
        Duration = CloseDuration,
        FillMode = FillMode.Forward,
        Children =
        {
            new KeyFrame
            {
                Cue = new Cue(0d),
                Setters =
                {
                    new Setter(TranslateTransform.YProperty, 0d),
                    new Setter(OpacityProperty, 1d),
                },
            },
            new KeyFrame
            {
                Cue = new Cue(1d),
                KeySpline = new KeySpline(1d, 0d, 1d, 1d),
                Setters =
                {
                    new Setter(TranslateTransform.YProperty, -SlideOffset),
                    new Setter(OpacityProperty, 0d),
                },
            },
        },
    };
}
