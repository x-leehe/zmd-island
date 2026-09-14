using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using EndfieldCharge.Animations;
using EndfieldCharge.Services;
using EndfieldCharge.Views;
using DesignTokens = EndfieldCharge.Host.Island.Animation.DesignTokens;

namespace EndfieldCharge.Settings;

/// <summary>
/// 设置抽屉：独立可激活的无边框圆角面板，锚定在灵动岛正下方滑出（视觉与动效同右键菜单）。
/// 六个分段页签（复用原设置语义 + 新增「开发者」占位页），控件改动即时保存；
/// 「收起」键 / Esc / 失焦三条路径都会再保存一次并播退场动画后关闭。
/// </summary>
public partial class SettingsWindow : Window
{
    // ---- 设计常量（DIP，取自 docs/designs/settings-all.json 的面板部分：整窗 750 − 岛体 74） ----
    private const double DesignPanelHeight = 676d;
    private const double PanelWidth = DesignTokens.PillWidth; // 面板可见宽 = 岛胶囊宽（同一 token，永不漂移）
    private const double ShadowPad = 10d;      // ContentRoot 四周透明边距：给岛同款阴影留位，避免被窗口裁掉
    private const double IslandGap = 8d;       // 面板上缘距岛下缘
    private const double ScreenMargin = 12d;   // 面板底部至少保留的屏幕边距
    private const double SlideOffset = 12d;    // 滑出定位移（从岛边缘方向）
    private const double FocusGraceMs = 250d;  // 打开后忽略虚假失焦的宽限期

    private static readonly TimeSpan OpenDuration = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan CloseDuration = TimeSpan.FromMilliseconds(150);

    private readonly HudWindow _hud;
    private readonly List<(Border Button, TextBlock Text, Control Panel, string Key)> _tabs = new();
    private readonly List<(Slider Slider, NumericUpDown Stepper)> _sliderValues = new();

    private CancellationTokenSource? _animCts;
    private bool _closing;
    private bool _loading;
    private bool _syncing;   // 滑块 ↔ 步进器同步中：阻止互相回灌
    private bool _activatedOnce;
    private bool _dialogOpen;
    private DateTime _activatedAtUtc;

    private double _globalScale = 0.8d;        // 全局缩放：打开时取自设置，改动即时重排
    private PixelRect _islandRect;             // 岛胶囊（物理像素）——重排时复用
    private Avalonia.Platform.Screen? _screen; // 目标显示器

    /// <summary>抽屉真正关闭后触发（退场动画已播完），宿主据此恢复岛的空闲计时。</summary>
    public event Action? DrawerClosed;

    public SettingsWindow(AppSettings settings, HudWindow hud, string initialTab = "General")
    {
        InitializeComponent();

        _hud = hud;

        // 窗口图标
        try
        {
            using var stream = AssetLoader.Open(new Uri("avares://EndfieldCharge/Assets/tray_bolt.png"));
            Icon = new WindowIcon(new Bitmap(stream));
        }
        catch { }

        Title = Localization.SettingsTitle;

        // 页签注册（顺序 = 分段控件顺序：通用 / 动画 / 通知 / 插件 / 开发者 / 关于）
        _tabs.Add((TabGeneralBtn, TabGeneralText, GeneralPanel, "General"));
        _tabs.Add((TabAnimationBtn, TabAnimationText, AnimationPanel, "Animation"));
        _tabs.Add((TabNotificationsBtn, TabNotificationsText, NotificationsPanel, "Notifications"));
        _tabs.Add((TabPluginsBtn, TabPluginsText, PluginsPanel, "Plugins"));
        _tabs.Add((TabDeveloperBtn, TabDeveloperText, DeveloperPanel, "Developer"));
        _tabs.Add((TabAboutBtn, TabAboutText, AboutPanel, "About"));

        foreach (var (button, _, _, key) in _tabs)
            button.PointerPressed += (_, _) => SwitchTab(key);

        InitLanguageCombo();
        InitPositionCombo();
        InitPreviewModeCombo();
        InitWheelSwitchCombo();
        ApplyLocalization();
        InitPluginPages();

        PopulateMonitors();
        WireEvents();

        // 加载期间控件会触发变更事件：只同步显示、不落盘
        _loading = true;
        LoadSettings(settings);
        _loading = false;

        _globalScale = Math.Clamp(settings.GlobalScale, 0.1d, 2d);

        RefreshValueTexts();
        SwitchTab(initialTab);
    }

    // ---------------- 事件接线（控件改动即时保存） ----------------

    private void WireEvents()
    {
        WireSlider(ScaleSlider, ScaleStepper, "{0:F2}");

        // 全局缩放即时生效：整窗跟随缩放并重排锚点（内容 / 圆角 / 阴影一起缩）
        ScaleSlider.PropertyChanged += (_, e) =>
        {
            if (e.Property == RangeBase.ValueProperty)
            {
                _globalScale = Math.Clamp(ScaleSlider.Value, 0.1d, 2d);
                ApplyLayout(initial: false);
            }
        };
        WireSlider(WaitingTimeoutSlider, WaitingTimeoutStepper, "{0:F1}");
        WireSlider(ContractedTimeoutSlider, ContractedTimeoutStepper, "{0:F1}");
        WireSlider(BounceSlider, BounceStepper, "{0:F3}");
        WireSlider(RippleIntensitySlider, RippleIntensityStepper, "{0:F2}");
        WireSlider(RippleSpreadSlider, RippleSpreadStepper, "{0:F2}");
        WireSlider(LowBatterySlider, LowBatteryStepper, "{0:F0}");

        WindowTopmostSwitch.IsCheckedChanged += (_, _) => SaveInstant();
        AutoStartSwitch.IsCheckedChanged += (_, _) => SaveInstant();
        PowerSaverSwitch.IsCheckedChanged += (_, _) => SaveInstant();
        FullChargeSwitch.IsCheckedChanged += (_, _) => SaveInstant();
        LowBatterySwitch.IsCheckedChanged += (_, _) =>
        {
            LowBatterySlider.IsEnabled = LowBatterySwitch.IsChecked == true;
            SaveInstant();
        };

        WheelSwitchCombo.SelectionChanged += (_, _) => SaveInstant();
        PositionCombo.SelectionChanged += (_, _) => SaveInstant();
        MonitorCombo.SelectionChanged += (_, _) => SaveInstant();
        LanguageCombo.SelectionChanged += (_, _) =>
        {
            SaveInstant();
            ApplyLocalization(); // 语言即时生效：本窗文案随新语言刷新
        };

        // 底部动作行
        SaveBtn.Click += OnSave;
        CollapseBtn.Click += (_, _) => RequestClose();

        // 关闭路径：Esc / 失焦（见 OnWindowDeactivated）
        KeyDown += OnWindowKeyDown;
        Activated += (_, _) =>
        {
            _activatedOnce = true;
            _activatedAtUtc = DateTime.UtcNow;
        };
        Deactivated += OnWindowDeactivated;

        CheckUpdateBtn.Click += OnCheckUpdate;
        PreviewPlayBtn.Click += OnPlayPreview;
        FontInstallBtn.Click += OnInstallFont;
    }

    /// <summary>滑块 + 步进器双向绑定：两者编辑同一个值，任一改动即时保存。</summary>
    private void WireSlider(Slider slider, NumericUpDown stepper, string format)
    {
        _sliderValues.Add((slider, stepper));

        stepper.FormatString = format;
        stepper.Value = ToDecimal(slider.Value);

        slider.PropertyChanged += (_, e) =>
        {
            if (e.Property != RangeBase.ValueProperty || _syncing)
                return;

            _syncing = true;
            stepper.Value = ToDecimal(slider.Value);
            _syncing = false;

            SaveInstant();
        };

        stepper.PropertyChanged += (_, e) =>
        {
            if (e.Property != NumericUpDown.ValueProperty || _syncing || stepper.Value is not decimal v)
                return;

            _syncing = true;
            slider.Value = Math.Clamp((double)v, slider.Minimum, slider.Maximum);
            _syncing = false;

            SaveInstant();
        };
    }

    private static decimal ToDecimal(double value) => (decimal)Math.Round(value, 6);

    /// <summary>加载设置后刷新一次数值文本（滑块默认值与设置值相同时不会触发 PropertyChanged）。</summary>
    private void RefreshValueTexts()
    {
        foreach (var (slider, stepper) in _sliderValues)
            stepper.Value = ToDecimal(slider.Value);
    }

    /// <summary>Esc = 收起（与「收起」键同一路径）。</summary>
    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || e.Handled)
            return;

        e.Handled = true;
        RequestClose();
    }

    // ---------------- 初始化 ComboBox 项 ----------------

    private void InitLanguageCombo()
    {
        LanguageCombo.Items.Clear();
        LanguageCombo.Items.Add(new ComboBoxItem { Tag = "auto" });
        LanguageCombo.Items.Add(new ComboBoxItem { Tag = "zh" });
        LanguageCombo.Items.Add(new ComboBoxItem { Tag = "en" });
    }

    private void InitPositionCombo()
    {
        PositionCombo.Items.Clear();
        PositionCombo.Items.Add(new ComboBoxItem { Tag = "TopCenter" });
        PositionCombo.Items.Add(new ComboBoxItem { Tag = "TopRight" });
        PositionCombo.Items.Add(new ComboBoxItem { Tag = "TopLeft" });
    }

    private void InitPreviewModeCombo()
    {
        PreviewModeCombo.Items.Clear();
        PreviewModeCombo.Items.Add(new ComboBoxItem { Tag = "plug" });
        PreviewModeCombo.Items.Add(new ComboBoxItem { Tag = "saver" });
        PreviewModeCombo.Items.Add(new ComboBoxItem { Tag = "unplug" });
    }

    private void InitWheelSwitchCombo()
    {
        WheelSwitchCombo.Items.Clear();
        WheelSwitchCombo.Items.Add(new ComboBoxItem { Tag = "Wrap" });
        WheelSwitchCombo.Items.Add(new ComboBoxItem { Tag = "Clamp" });
        WheelSwitchCombo.Items.Add(new ComboBoxItem { Tag = "Disabled" });
    }

    // ---------------- 本地化 ----------------

    private void ApplyLocalization()
    {
        WinTitle.Text = Localization.SettingsTitle;
        WordmarkText.Text = Localization.Wordmark;

        TabGeneralText.Text = Localization.TabGeneral;
        TabAnimationText.Text = Localization.TabAnimation;
        TabNotificationsText.Text = Localization.TabNotifications;
        TabPluginsText.Text = Localization.Plugins;
        TabDeveloperText.Text = Localization.TabDeveloper;
        TabAboutText.Text = Localization.TabAbout;

        // 分段页签按设计 fs18；英文页签名长得多，收一号避免被段宽截断
        double tabFontSize = Localization.IsChineseUi ? 18d : 13d;
        foreach (var (_, text, _, _) in _tabs)
            text.FontSize = tabFontSize;

        SectionDisplayText.Text = Localization.SectionDisplay;
        SectionPositionText.Text = Localization.SectionPosition;
        SectionStartupText.Text = Localization.SectionStartup;
        SectionAnimParams.Text = Localization.SectionAnimParams;
        SectionPreview.Text = Localization.SectionPreview;
        SectionAlertSettingsText.Text = Localization.SectionAlertSettings;
        SectionPluginsText.Text = Localization.Plugins;
        SectionDeveloperText.Text = Localization.TabDeveloper;
        SectionAboutText.Text = Localization.TabAbout;

        LabelScale.Text = Localization.LabelScale;
        LabelTopmost.Text = Localization.Topmost;
        DescTopmostText.Text = Localization.DescTopmost;
        LabelWheelSwitch.Text = Localization.LabelWheelSwitch;
        DescWheelSwitchText.Text = Localization.DescWheelSwitch;
        LabelPosition.Text = Localization.LabelPosition;
        LabelMonitor.Text = Localization.LabelMonitor;
        LabelLanguage.Text = Localization.LabelLanguage;
        LabelAutoStart.Text = Localization.LabelAutoStart;
        DescAutoStartText.Text = Localization.DescAutoStart;

        LabelWaitingTimeout.Text = Localization.LabelWaitingTimeout;
        LabelContractedTimeout.Text = Localization.LabelContractedTimeout;
        LabelBounce.Text = Localization.LabelBounce;
        LabelRippleIntensity.Text = Localization.LabelRippleIntensity;
        LabelRippleSpread.Text = Localization.LabelRippleSpread;
        LabelPlayMode.Text = Localization.LabelPlayMode;

        LabelPowerSaverNotify.Text = Localization.LabelPowerSaverNotify;
        PowerSaverNotifyDesc.Text = Localization.PowerSaverNotifyDesc;
        LabelLowBatteryEnable.Text = Localization.LabelLowBatteryEnable;
        DescLowBatteryAlertText.Text = Localization.DescLowBatteryAlert;
        LabelLowBattery.Text = Localization.LabelLowBattery;
        LabelFullChargeEnable.Text = Localization.LabelFullChargeEnable;
        DescFullChargeAlertText.Text = Localization.DescFullChargeAlert;

        LabelVersion.Text = Localization.LabelVersion;
        LabelAuthor.Text = Localization.LabelAuthor;
        AboutSubtitleText.Text = Localization.AboutSubtitle;
        CheckUpdateBtn.Content = Localization.BtnCheckUpdate;
        FontSectionTitle.Text = Localization.FontSectionTitle;
        FontDescText.Text = Localization.FontDesc;
        FontInstallBtn.Content = Localization.BtnInstallFont;
        PreviewPlayBtn.Content = Localization.BtnPlay;
        DeveloperStubText.Text = Localization.DeveloperComingSoon;

        ToolTip.SetTip(SaveBtn, Localization.BtnSave);
        ToolTip.SetTip(CollapseBtn, Localization.BtnCollapse);

        if (LanguageCombo.Items.Count >= 3)
        {
            if (LanguageCombo.Items[0] is ComboBoxItem ci0) ci0.Content = Localization.ValueAuto;
            if (LanguageCombo.Items[1] is ComboBoxItem ci1) ci1.Content = Localization.ValueChinese;
            if (LanguageCombo.Items[2] is ComboBoxItem ci2) ci2.Content = Localization.ValueEnglish;
        }

        if (PositionCombo.Items.Count >= 3)
        {
            if (PositionCombo.Items[0] is ComboBoxItem pi0) pi0.Content = Localization.PosTopCenter;
            if (PositionCombo.Items[1] is ComboBoxItem pi1) pi1.Content = Localization.PosTopRight;
            if (PositionCombo.Items[2] is ComboBoxItem pi2) pi2.Content = Localization.PosTopLeft;
        }

        if (WheelSwitchCombo.Items.Count >= 3)
        {
            if (WheelSwitchCombo.Items[0] is ComboBoxItem wi0) wi0.Content = Localization.WheelSwitchWrap;
            if (WheelSwitchCombo.Items[1] is ComboBoxItem wi1) wi1.Content = Localization.WheelSwitchClamp;
            if (WheelSwitchCombo.Items[2] is ComboBoxItem wi2) wi2.Content = Localization.WheelSwitchDisabled;
        }

        if (PreviewModeCombo.Items.Count >= 3)
        {
            if (PreviewModeCombo.Items[0] is ComboBoxItem mi0) mi0.Content = Localization.ModePlug;
            if (PreviewModeCombo.Items[1] is ComboBoxItem mi1) mi1.Content = Localization.ModeSaver;
            if (PreviewModeCombo.Items[2] is ComboBoxItem mi2) mi2.Content = Localization.ModeUnplug;
        }
    }

    // ---------------- 显示器 ----------------

    private void PopulateMonitors()
    {
        var screens = Screens.All;
        MonitorCombo.Items.Clear();

        // 第一项：主显示器（默认），MonitorIndex 存 -1
        MonitorCombo.Items.Add(new ComboBoxItem
        {
            Content = Localization.MonitorPrimaryDefault,
            Tag = -1,
        });

        for (int i = 0; i < screens.Count; i++)
        {
            var s = screens[i];
            MonitorCombo.Items.Add(new ComboBoxItem
            {
                Content = Localization.MonitorName(i, s.IsPrimary),
                Tag = i,
            });
        }
    }

    // ---------------- 加载 / 收集 ----------------

    private void LoadSettings(AppSettings s)
    {
        ScaleSlider.Value = s.GlobalScale;
        WindowTopmostSwitch.IsChecked = s.WindowTopmost;
        WaitingTimeoutSlider.Value = s.WaitingTimeoutSeconds;
        ContractedTimeoutSlider.Value = s.ContractedTimeoutSeconds;
        BounceSlider.Value = s.BounceStrength;
        RippleIntensitySlider.Value = s.RippleIntensity;
        RippleSpreadSlider.Value = s.RippleSpread;
        PositionCombo.SelectedIndex = (int)s.HudPosition;
        WheelSwitchCombo.SelectedIndex = (int)s.WheelSwitch;
        // 下拉第一项是「主显示器（默认）」(-1)，物理显示器 i 对应下拉第 i+1 项
        int monitorSel = s.MonitorIndex < 0 ? 0 : s.MonitorIndex + 1;
        MonitorCombo.SelectedIndex = monitorSel >= 0 && monitorSel < MonitorCombo.Items.Count
            ? monitorSel
            : 0;

        LanguageCombo.SelectedIndex = s.Language switch
        {
            "zh" => 1,
            "en" => 2,
            _ => 0,
        };

        PowerSaverSwitch.IsChecked = s.EnablePowerSaverNotify;
        LowBatterySwitch.IsChecked = s.EnableLowBatteryAlert;
        LowBatterySlider.Value = s.LowBatteryThreshold;
        LowBatterySlider.IsEnabled = s.EnableLowBatteryAlert;
        FullChargeSwitch.IsChecked = s.EnableFullChargeAlert;
        AutoStartSwitch.IsChecked = s.EnableAutoStart;

        VersionText.Text = GetType().Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

        FontStatusText.Text = string.Empty;
    }

    private AppSettings CollectSettings() => new()
    {
        GlobalScale = Math.Round(ScaleSlider.Value, 2),
        WindowTopmost = WindowTopmostSwitch.IsChecked == true,
        WaitingTimeoutSeconds = Math.Round(WaitingTimeoutSlider.Value, 1),
        ContractedTimeoutSeconds = Math.Round(ContractedTimeoutSlider.Value, 1),
        BounceStrength = Math.Round(BounceSlider.Value, 3),
        RippleIntensity = Math.Round(RippleIntensitySlider.Value, 2),
        RippleSpread = Math.Round(RippleSpreadSlider.Value, 2),
        HudPosition = (HudPosition)PositionCombo.SelectedIndex,
        WheelSwitch = (WheelSwitchMode)Math.Clamp(WheelSwitchCombo.SelectedIndex, 0, 2),
        MonitorIndex = MonitorCombo.SelectedItem is ComboBoxItem item && item.Tag is int idx
            ? idx
            : 0,
        Language = LanguageCombo.SelectedIndex switch
        {
            1 => "zh",
            2 => "en",
            _ => "auto",
        },
        EnableLowBatteryAlert = LowBatterySwitch.IsChecked == true,
        LowBatteryThreshold = (int)LowBatterySlider.Value,
        EnableFullChargeAlert = FullChargeSwitch.IsChecked == true,
        EnablePowerSaverNotify = PowerSaverSwitch.IsChecked == true,
        EnableAutoStart = AutoStartSwitch.IsChecked == true,
    };

    // ---------------- 插件设置页 ----------------

    /// <summary>把所有贡献设置面板的插件面板依次堆叠展示；不做选择器（选择岛用鼠标滚轮）。</summary>
    private void InitPluginPages()
    {
        var pages = _hud.SettingsPages.ToList();

        if (pages.Count == 0)
        {
            NoPluginSettingsText.Text = Localization.NoPluginSettings;
            NoPluginSettingsText.IsVisible = true;
            return;
        }

        foreach (var page in pages)
            PluginPages.Children.Add(page.CreateSettingsView());
    }

    // ---------------- Tab 切换 ----------------

    private void SwitchTab(string key)
    {
        if (!_tabs.Exists(t => string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase)))
            key = "General";

        foreach (var (button, _, panel, tabKey) in _tabs)
        {
            bool active = string.Equals(tabKey, key, StringComparison.OrdinalIgnoreCase);
            button.Classes.Set("active", active);
            panel.IsVisible = active;
        }

        ContentScroll.Offset = new Vector(0, 0);
    }

    // ---------------- 动画预览 ----------------

    /// <summary>用当前滑块值（未保存也生效）实时预览动画。</summary>
    private async void OnPlayPreview(object? sender, RoutedEventArgs e)
    {
        PreviewPlayBtn.IsEnabled = false;

        // 用当前滑块值构造参数，无需保存即可预览效果（响应动画时长固定为基线节奏）
        var options = new AnimationOptions
        {
            BounceStrength = Math.Clamp(BounceSlider.Value, 0d, 0.5d),
            RippleIntensity = Math.Clamp(RippleIntensitySlider.Value, 0d, 2d),
            RippleSpread = Math.Clamp(RippleSpreadSlider.Value, 0.5d, 1.5d),
        };

        var sample = new BatterySnapshot(
            RemainingWh: 62.4, FullWh: 90.0,
            Percent: 69, AcOnline: true, Charging: true);

        try
        {
            switch (PreviewModeCombo.SelectedIndex)
            {
                case 1:
                    await _hud.ShowAndPlayAsync(sample, acOnline: true,
                        HudPlayMode.PowerSaver, options);
                    break;
                case 2:
                    await _hud.ShowSimpleAsync(sample, options);
                    break;
                default:
                    await _hud.ShowAndPlayAsync(sample, acOnline: true,
                        HudPlayMode.Charge, options);
                    break;
            }
        }
        catch
        {
        }
        finally
        {
            PreviewPlayBtn.IsEnabled = true;
        }
    }

    // ---------------- 保存 ----------------

    /// <summary>控件改动即时保存：只落盘 + 生效，不弹「已保存」提示。</summary>
    private void SaveInstant()
    {
        if (_loading)
            return;

        Save(showToast: false);
    }

    private void OnSave(object? sender, RoutedEventArgs e) => Save(showToast: true);

    /// <summary>收集当前控件值 → 持久化 → 广播生效；showToast 仅「保存」键需要。</summary>
    private void Save(bool showToast)
    {
        var settings = CollectSettings();
        SettingsManager.Save(settings);

        // 处理开机自启
        if (settings.EnableAutoStart)
            Services.AutoStart.Enable(Services.AutoStart.CurrentExePath);
        else
            Services.AutoStart.Disable();

        if (Application.Current is App app)
            app.OnSettingsChanged(settings);

        if (!showToast)
            return;

        SavedHint.Text = Localization.SavedToast;
        SavedHint.Opacity = 1;
        Dispatcher.UIThread.Post(async () =>
        {
            await Task.Delay(2000);
            SavedHint.Opacity = 0;
        });
    }

    // ---------------- 检查更新 ----------------

    private async void OnCheckUpdate(object? sender, RoutedEventArgs e)
    {
        CheckUpdateBtn.IsEnabled = false;
        UpdateStatusText.Text = "...";

        try
        {
            var (hasUpdate, version, url) = await Services.UpdateChecker.CheckAsync();
            if (hasUpdate && url is not null)
            {
                // 模态对话框会让本窗失焦：期间不能把失焦当成「点了别处」而收起抽屉
                _dialogOpen = true;
                MessageBoxResult result;
                try
                {
                    result = await MessageBox.Show(
                        this,
                        Localization.UpdateMsg(version ?? "?"),
                        Localization.UpdateTitle,
                        MessageBoxButton.OkCancel);
                }
                finally
                {
                    _dialogOpen = false;
                }

                if (result == MessageBoxResult.Ok)
                    Platform.Start(url);
            }
            else
            {
                UpdateStatusText.Text = Localization.UpToDate;
            }
        }
        catch
        {
            UpdateStatusText.Text = Localization.UpdateCheckFailed;
        }
        finally
        {
            CheckUpdateBtn.IsEnabled = true;
        }
    }

    // ---------------- 字体安装 ----------------

    private async void OnInstallFont(object? sender, RoutedEventArgs e)
    {
        FontInstallBtn.IsEnabled = false;
        FontStatusText.Text = Localization.FontInstalling;

        // Inter 字体 GitHub Releases 下载页
        const string fontUrl = "https://github.com/rsms/inter/releases/latest";

        try
        {
            Platform.Start(fontUrl);
            await Task.Delay(500);
            FontStatusText.Text = Localization.FontInstalled;
        }
        catch
        {
            FontStatusText.Text = Localization.UpdateCheckFailed;
        }
        finally
        {
            FontInstallBtn.IsEnabled = true;
        }
    }

    // ---------------- 抽屉定位（锚定岛下缘） ----------------

    /// <summary>
    /// 在灵动岛正下方显示抽屉：水平居中于岛，夹紧到目标显示器工作区；
    /// 整窗按全局缩放渲染（<see cref="ScaleHost"/>），高度取 min(设计高, 工作区可用高)，超出由滚动条承担。
    /// </summary>
    public void ShowBelowIsland(PixelRect islandRect, Avalonia.Platform.Screen screen)
    {
        _islandRect = islandRect;
        _screen = screen;

        ApplyLayout(initial: true);
        Show();
    }

    /// <summary>按全局缩放 + 屏幕工作区重算窗口尺寸与锚点（面板上缘贴岛下缘 + 8，水平居中）。</summary>
    private void ApplyLayout(bool initial)
    {
        if (_screen is null)
            return;

        double scaling = _screen.Scaling > 0 ? _screen.Scaling : 1d;
        var area = _screen.WorkingArea;
        double dip = Math.Clamp(_globalScale, 0.1d, 2d);

        // 关键：缩放只做一次。ScaleRoot 以「设计 DIP」布局、按 dip 渲染；窗口尺寸 = 设计 × dip。
        // 若窗口也按 dip 拉大再叠加渲染缩放，就会缩放两次（内容既不等于岛宽，也不同步）。
        if (ScaleRoot.RenderTransform is ScaleTransform scale)
        {
            scale.ScaleX = dip;
            scale.ScaleY = dip;
        }
        else
        {
            ScaleRoot.RenderTransform = new ScaleTransform(dip, dip);
        }

        double designW = PanelWidth + (2 * ShadowPad);
        ScaleRoot.Width = designW;
        Width = designW * dip;

        int padPx = (int)Math.Round(ShadowPad * dip * scaling);
        int winW = (int)Math.Round(designW * dip * scaling);
        int gapPx = (int)Math.Round(IslandGap * dip * scaling);
        int marginPx = (int)Math.Round(ScreenMargin * scaling);

        // 水平居中于岛；上缘 = 岛下缘 + 8 再上移一个阴影边距（让「可见面板」而非窗口贴岛）
        int x = _islandRect.X + ((_islandRect.Width - winW) / 2);
        int y = _islandRect.Bottom + gapPx - padPx;

        int available = Math.Max(0, area.Bottom - y - marginPx);
        int designH = (int)Math.Round((DesignPanelHeight + (2 * ShadowPad)) * dip * scaling);
        int winH = Math.Min(designH, available);
        Height = winH / scaling;
        ScaleRoot.Height = Height / dip;   // 设计 DIP：渲染缩放后正好与窗口等高

        x = Math.Clamp(x, area.X, Math.Max(area.X, area.Right - winW));
        y = Math.Clamp(y, area.Y, Math.Max(area.Y, area.Bottom - winH));

        Position = new PixelPoint(x, y);

        if (initial)
            Services.Logger.Info($"SettingsDrawer: island={_islandRect} wa={area} pos=({x},{y}) size={winW}×{winH} scale={dip:F2}");
    }

    // ---------------- 关闭（收起键 / Esc / 失焦共用） ----------------

    /// <summary>统一关闭入口：先保存一次，再播退场动画后 Close。</summary>
    public void RequestClose()
    {
        if (_closing)
            return;

        _closing = true;

        // 任何关闭路径都再存一次（语言等刚改、滑块可能还在拖动）
        Save(showToast: false);

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

    /// <summary>开场：从岛边缘（上）滑出 + 淡入，200ms KeySpline(0,1,1,1)。</summary>
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

    /// <summary>退场：缩回岛边缘 + 淡出，150ms KeySpline(1,0,1,1)。</summary>
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

    // ---------------- 窗口生命周期 ----------------

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        _activatedAtUtc = DateTime.UtcNow;
        _animCts = new CancellationTokenSource();
        _ = BuildOpenAnimation().RunAsync(ContentRoot, _animCts.Token);
    }

    /// <summary>
    /// 失焦即收起（与右键菜单不同：抽屉要接收键盘输入，必须可激活）。
    /// 打开瞬间会有一次虚假 Deactivated：宽限期内或从未激活过都忽略。
    /// </summary>
    private void OnWindowDeactivated(object? sender, EventArgs e)
    {
        if (_closing || !_activatedOnce || _dialogOpen)
            return;

        if ((DateTime.UtcNow - _activatedAtUtc).TotalMilliseconds < FocusGraceMs)
            return;

        // 下拉弹窗可能瞬时改变激活态：下一拍再确认，真的失焦才收
        Dispatcher.UIThread.Post(() =>
        {
            if (!_closing && IsVisible && !IsActive)
                RequestClose();
        }, DispatcherPriority.Background);
    }

    protected override void OnClosed(EventArgs e)
    {
        _animCts?.Cancel();

        // 兜底：未走 RequestClose 的关闭路径（应用退出等）也要保存一次
        if (!_closing)
            Save(showToast: false);

        DrawerClosed?.Invoke();

        base.OnClosed(e);
    }
}

// 极简消息框辅助
public enum MessageBoxButton { Ok, OkCancel }
public enum MessageBoxResult { Ok, Cancel }

public static class MessageBox
{
    public static async Task<MessageBoxResult> Show(
        Window owner, string message, string title,
        MessageBoxButton button = MessageBoxButton.Ok)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 380,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
            Foreground = Brushes.White,
            CanResize = false,
            SystemDecorations = SystemDecorations.None,
            FontFamily = new FontFamily("HarmonyOS Sans SC, HarmonyOS Sans, Inter, Microsoft YaHei UI, sans-serif"),
        };

        var result = MessageBoxResult.Ok;
        var stack = new StackPanel { Margin = new Thickness(20), Spacing = 16 };
        stack.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        });

        var btnPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Spacing = 8,
        };

        var okBtn = new Button
        {
            Content = Localization.BtnDownload,
            Width = 80,
            Height = 32,
            Background = new SolidColorBrush(Color.Parse("#C6CA4C")),
            Foreground = new SolidColorBrush(Color.Parse("#1E1E1E")),
            FontWeight = FontWeight.SemiBold,
            CornerRadius = new CornerRadius(6),
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center,
        };
        okBtn.Click += (_, _) => { result = MessageBoxResult.Ok; dialog.Close(); };
        btnPanel.Children.Add(okBtn);

        if (button == MessageBoxButton.OkCancel)
        {
            var cancelBtn = new Button
            {
                Content = Localization.BtnCancel,
                Width = 80,
                Height = 32,
                CornerRadius = new CornerRadius(6),
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center,
            };
            cancelBtn.Click += (_, _) => { result = MessageBoxResult.Cancel; dialog.Close(); };
            btnPanel.Children.Insert(0, cancelBtn);
        }

        stack.Children.Add(btnPanel);
        dialog.Content = stack;
        dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;

        await dialog.ShowDialog(owner);
        return result;
    }
}

internal static class Platform
{
    public static void Start(string url)
    {
        using var p = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            },
        };
        p.Start();
    }
}
