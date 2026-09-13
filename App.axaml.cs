using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using EndfieldCharge.Contracts;
using EndfieldCharge.Host.Menu;
using EndfieldCharge.Host.Plugins;
using EndfieldCharge.Host.Plugins.Battery;
using EndfieldCharge.Host.Plugins.Demo;
using EndfieldCharge.Services;
using EndfieldCharge.Settings;
using EndfieldCharge.Views;

namespace EndfieldCharge;

public partial class App : Application
{
    private PowerWatcher? _watcher;
    private HudWindow? _hud;
    private TrayIcon? _tray;
    private NativeMenuItem? _trayTopmostItem;   // 原生右键菜单「置顶」项（供设置变更时刷新勾选）
    private PluginRegistry? _pluginRegistry;    // 进程内插件注册表
    private AppSettings _settings = new();
    private IClassicDesktopStyleApplicationLifetime? _desktop;
    private bool _lastLowBatteryNotified;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return;

        _desktop = desktop;

        // 加载设置
        _settings = SettingsManager.Load();
        Localization.UseSettings(_settings);
        Logger.Enabled = true; // 可改为设置项

        // 全局未捕获异常兜底
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Logger.Error(e.ExceptionObject as Exception ?? new Exception("Unknown unhandled error"));
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Logger.Error(e.Exception);
            e.SetObserved();
        };

        desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        desktop.Exit += OnDesktopExit;

        // 插件注册表：电池元插件（皮肤/内容，不可卸载）+ 音乐演示插件（菜单注入）
        _pluginRegistry = new PluginRegistry();
        _pluginRegistry.Register(new BatteryPlugin());
        _pluginRegistry.Register(new MusicDemoPlugin());

        _hud = new HudWindow(_pluginRegistry);
        _hud.ApplySettings(_settings);

        SetupTrayIcon();
        StartPowerWatching();

        // 调试命令行参数
        if (HasCommandLineArg("--demo"))
            _ = PreviewWithSampleDataAsync();
        else if (HasCommandLineArg("--preview-unplug"))
            _ = PreviewSimpleAsync();
        else if (HasCommandLineArg("--preview"))
            _ = TriggerHudAsync();

        base.OnFrameworkInitializationCompleted();
    }

    // ---------------- 设置 ----------------

    public void OnSettingsChanged(AppSettings settings)
    {
        _settings = settings;
        Localization.UseSettings(settings);
        _hud?.ApplySettings(settings);

        // 同步原生右键菜单「置顶」勾选状态（岛菜单 / 设置窗口切换后保持一致）
        if (_trayTopmostItem is not null)
            _trayTopmostItem.IsChecked = settings.WindowTopmost;

        // 更新托盘提示
        if (_tray is not null)
            _tray.ToolTipText = Localization.TrayTooltip;
    }

    // ---------------- 命令行参数 ----------------

    private static bool HasCommandLineArg(string name)
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 1; i < args.Length; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    // ---------------- 预览 ----------------

    private async Task PreviewWithSampleDataAsync()
    {
        if (_hud is null) return;

        var sample = new BatterySnapshot(
            RemainingWh: 62.4, FullWh: 90.0,
            Percent: 69, AcOnline: true, Charging: true);

        await _hud.ShowAndPlayAsync(sample, acOnline: true);
    }

    private async Task PreviewSimpleAsync()
    {
        if (_hud is null) return;

        var sample = new BatterySnapshot(
            RemainingWh: 62.4, FullWh: 90.0,
            Percent: 69, AcOnline: false, Charging: false);

        await _hud.ShowSimpleAsync(sample);
    }

    // ---------------- 电源监听 ----------------

    private void StartPowerWatching()
    {
        _watcher = new PowerWatcher();

        _watcher.PowerSourceChanged += (_, acOnline) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (acOnline)
                    _ = TriggerHudAsync();
                else
                    _ = TriggerSimpleHudAsync();
            });
        };

        // 省电模式开关：开启 → 完整三态（省电文案）；关闭 → 简化电量胶囊
        _watcher.PowerSavingChanged += (_, enabled) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (!_settings.EnablePowerSaverNotify)
                    return;

                if (enabled)
                    _ = TriggerSaverHudAsync();
                else
                    _ = TriggerSimpleHudAsync();
            });
        };

        _watcher.Start();
    }

    private async Task TriggerSaverHudAsync()
    {
        if (_hud is null) return;

        var snapshot = await Task.Run(() => BatteryService.GetSnapshot());
        await _hud.ShowAndPlayAsync(snapshot, acOnline: true, HudPlayMode.PowerSaver);
    }

    private async Task TriggerSimpleHudAsync()
    {
        if (_hud is null) return;

        var (snapshot, _) = await Task.Run(() =>
        {
            PowerNative.TryGetAcOnline(out bool ac);
            return (BatteryService.GetSnapshot(), ac);
        });

        await _hud.ShowSimpleAsync(snapshot);
    }

    private async Task TriggerHudAsync()
    {
        if (_hud is null) return;

        var (snapshot, acOnline) = await Task.Run(() =>
        {
            PowerNative.TryGetAcOnline(out bool ac);
            return (BatteryService.GetSnapshot(), ac);
        });

        await _hud.ShowAndPlayAsync(snapshot, acOnline);

        // 检查提醒条件
        if (snapshot is not null)
            CheckAlerts(snapshot);
    }

    /// <summary>检查并触发低电量 / 充满提醒。</summary>
    private void CheckAlerts(BatterySnapshot snap)
    {
        if (!snap.HasBattery) return;

        // 充满提醒（充电中且 >= 99%）
        if (_settings.EnableFullChargeAlert && snap.Charging && snap.Percent >= 99)
        {
            _ = ShowAlertAsync(Localization.FullChargeTitle, Localization.FullChargeMsg);
        }

        // 低电量提醒（放电中且低于阈值，每轮只提醒一次）
        if (_settings.EnableLowBatteryAlert && !snap.Charging && snap.Percent <= _settings.LowBatteryThreshold)
        {
            if (!_lastLowBatteryNotified)
            {
                _lastLowBatteryNotified = true;
                _ = ShowAlertAsync(Localization.LowBatteryTitle, Localization.LowBatteryMsg(snap.Percent));
            }
        }
        else
        {
            _lastLowBatteryNotified = false;
        }
    }

    /// <summary>弹出一个卡牌风格提醒窗口，4 秒后自动消失。</summary>
    private static async Task ShowAlertAsync(string title, string message)
    {
        var alert = new Window
        {
            Title = title,
            Width = 340, Height = 140,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#18181A")),
            Foreground = Avalonia.Media.Brushes.White,
            CanResize = false,
            SystemDecorations = SystemDecorations.BorderOnly,
            Topmost = true,
            FontFamily = new Avalonia.Media.FontFamily("HarmonyOS Sans SC, HarmonyOS Sans, Inter, Microsoft YaHei UI, sans-serif"),
        };

        var card = new Border
        {
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#232325")),
            CornerRadius = new Avalonia.CornerRadius(10),
            Margin = new Avalonia.Thickness(12),
            Padding = new Avalonia.Thickness(18, 16),
        };

        var stack = new StackPanel { Spacing = 10 };
        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 15,
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
            Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#C6CA4C")),
        });
        stack.Children.Add(new TextBlock
        {
            Text = message,
            FontSize = 13,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#C8C8C8")),
        });
        card.Child = stack;
        alert.Content = card;

        // 入场动画
        alert.Opacity = 0;
        alert.RenderTransform = new Avalonia.Media.ScaleTransform(0.92, 0.92);
        alert.RenderTransformOrigin = new Avalonia.RelativePoint(0.5, 0.5, Avalonia.RelativeUnit.Relative);

        alert.Show();

        var fadeIn = new Avalonia.Animation.Animation
        {
            Duration = TimeSpan.FromMilliseconds(150),
            FillMode = Avalonia.Animation.FillMode.Forward,
            Easing = new Avalonia.Animation.Easings.QuadraticEaseOut(),
            Children =
            {
                new Avalonia.Animation.KeyFrame { Cue = new Avalonia.Animation.Cue(0d), Setters = { new Avalonia.Styling.Setter(Avalonia.Visual.OpacityProperty, 0d) } },
                new Avalonia.Animation.KeyFrame { Cue = new Avalonia.Animation.Cue(1d), Setters = { new Avalonia.Styling.Setter(Avalonia.Visual.OpacityProperty, 1d) } },
            },
        };
        _ = fadeIn.RunAsync(alert);

        await Task.Delay(4000);

        // 退场
        var fadeOut = new Avalonia.Animation.Animation
        {
            Duration = TimeSpan.FromMilliseconds(120),
            FillMode = Avalonia.Animation.FillMode.Forward,
            Easing = new Avalonia.Animation.Easings.QuadraticEaseIn(),
            Children =
            {
                new Avalonia.Animation.KeyFrame { Cue = new Avalonia.Animation.Cue(0d), Setters = { new Avalonia.Styling.Setter(Avalonia.Visual.OpacityProperty, 1d) } },
                new Avalonia.Animation.KeyFrame { Cue = new Avalonia.Animation.Cue(1d), Setters = { new Avalonia.Styling.Setter(Avalonia.Visual.OpacityProperty, 0d) } },
            },
        };
        await fadeOut.RunAsync(alert);
        if (alert.IsVisible)
            alert.Close();
    }

    // ---------------- 托盘 ----------------

    private void SetupTrayIcon()
    {
        // 左键：自定义 Avalonia 菜单（TrayMenuWindow），沿用 OnTrayClicked；
        // 右键：原生 Win32 菜单（TrayIcon.Menu = NativeMenu，仅 Menu 非空时右键才弹原生菜单）。
        _tray = new TrayIcon
        {
            ToolTipText = Localization.TrayTooltip,
            IsVisible = true,
        };

        _tray.Clicked += OnTrayClicked;
        _tray.Menu = BuildTrayNativeMenu();

        try
        {
            var uri = new Uri("avares://EndfieldCharge/Assets/tray_bolt.png");
            using var stream = AssetLoader.Open(uri);
            _tray.Icon = new WindowIcon(new Bitmap(stream));
        }
        catch
        {
        }

        var icons = new TrayIcons { _tray };
        TrayIcon.SetIcons(this, icons);
    }

    /// <summary>
    /// 右键原生菜单（插件驱动）：
    /// A 类宿主固定项 = 显示 / 设置 / 置顶(可勾选) / 插件▸ / 关于 / 退出；
    /// B 类「插件 ▸」子菜单行由 PluginMenuComposer 从注册表生成（元插件锁定）。
    /// </summary>
    private NativeMenu BuildTrayNativeMenu()
    {
        if (_pluginRegistry is null)
            return new NativeMenu();

        var pluginChildren = PluginMenuComposer.BuildPluginChildren(MenuTarget.Tray, _pluginRegistry);

        var hostFixed = new List<MenuContribution>
        {
            new()
            {
                Id = "tray.show",
                Header = Localization.Show,
                Target = MenuTarget.Tray,
                Section = MenuSection.HostFixed,
                Priority = 0,
                Command = () => _ = TriggerHudAsync(),
            },
            new()
            {
                Id = "tray.settings",
                Header = Localization.Settings,
                Target = MenuTarget.Tray,
                Section = MenuSection.HostFixed,
                Priority = 1,
                Command = () => OpenSettingsWindow(),
            },
            new()
            {
                Id = "tray.topmost",
                Header = Localization.Topmost,
                Target = MenuTarget.Tray,
                Section = MenuSection.HostFixed,
                Priority = 2,
                IsChecked = _settings.WindowTopmost,
                Command = ToggleWindowTopmost,
            },
            new()
            {
                Id = "tray.plugins",
                Header = Localization.Plugins,
                Target = MenuTarget.Tray,
                Section = MenuSection.HostFixed,
                Priority = 3,
                Children = pluginChildren,
            },
            new()
            {
                Id = "tray.about",
                Header = Localization.About,
                Target = MenuTarget.Tray,
                Section = MenuSection.HostFixed,
                Priority = 4,
                Command = () => OpenSettingsWindow("About"),
            },
            new()
            {
                Id = "tray.exit",
                Header = Localization.Exit,
                Target = MenuTarget.Tray,
                Section = MenuSection.HostFixed,
                Priority = 5,
                Command = () => _desktop?.Shutdown(),
            },
        };

        var composed = PluginMenuComposer.Compose(MenuTarget.Tray, _pluginRegistry, hostFixed);
        return BuildNativeMenu(composed);
    }

    /// <summary>把合成结果转成原生菜单树（分隔线 / 复选框 / 子菜单 / 命令）。</summary>
    private NativeMenu BuildNativeMenu(IReadOnlyList<MenuContribution> items)
    {
        var menu = new NativeMenu();
        foreach (var item in items)
        {
            if (item.IsSeparator)
            {
                menu.Items.Add(new NativeMenuItemSeparator());
                continue;
            }

            bool checkable = item.IsChecked || item.Id.Contains("topmost", StringComparison.Ordinal);
            var native = new NativeMenuItem
            {
                Header = item.Header,
                IsEnabled = item.IsEnabled,
                ToggleType = checkable ? NativeMenuItemToggleType.CheckBox : NativeMenuItemToggleType.None,
                IsChecked = item.IsChecked,
            };

            if (item.Children is { Count: > 0 })
                native.Menu = BuildNativeMenu(item.Children);

            if (item.Command is not null)
                native.Click += (_, _) => item.Command();

            if (item.Id == "tray.topmost")
                _trayTopmostItem = native; // 供 OnSettingsChanged 刷新勾选

            menu.Items.Add(native);
        }

        return menu;
    }

    /// <summary>「置顶」切换：翻转设置 → 持久化 → 广播（HUD.ApplySettings 生效）。</summary>
    private void ToggleWindowTopmost()
    {
        var updated = _settings with { WindowTopmost = !_settings.WindowTopmost };
        SettingsManager.Save(updated);
        OnSettingsChanged(updated);
    }

    private void OnTrayClicked(object? sender, EventArgs e)
    {
        // 左键：等待态开关——已显示则隐藏；未显示则显示状态 C 并保持可见（无自动隐藏）。
        // 左键不再弹出自定义 TrayMenuWindow（该类保留供日后复用）；右键仍是原生 Win32 菜单。
        if (_hud is null)
            return;

        if (_hud.IsIslandVisible)
            _hud.HideIsland();
        else
            _ = _hud.ShowWaitingAsync();
    }

    private void OpenSettingsWindow(string initialTab = "General")
    {
        // _hud 在 OnFrameworkInitializationCompleted 中先于托盘创建，此处必非空
        var win = new SettingsWindow(_settings, _hud!, initialTab);
        win.Show();
    }

    // ---------------- 收尾 ----------------

    private void OnDesktopExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        _watcher?.Dispose();
        _watcher = null;

        if (_tray is not null)
        {
            _tray.IsVisible = false;
            _tray.Dispose();
            _tray = null;
        }
    }
}