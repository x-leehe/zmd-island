using System.Threading;

namespace EndfieldCharge;

/// <summary>
/// 本地化：支持运行时语言切换。
/// 优先使用 settings.Language，其次系统 UI 语言。
/// </summary>
public static class Localization
{
    private static string? _language;

    /// <summary>由宿主注入语言（设置项 Language；null = 跟随系统）。不依赖 App 的 Settings 类型。</summary>
    public static void UseLanguage(string? language) => _language = language;

    private static bool IsChinese
    {
        get
        {
            if (_language is not null && _language != "auto")
                return _language.StartsWith("zh");
            return Thread.CurrentThread.CurrentUICulture.Name.StartsWith("zh");
        }
    }

    /// <summary>当前界面是否为中文（供需要按语言微调排版的窗口使用，如设置抽屉的页签字号）。</summary>
    public static bool IsChineseUi => IsChinese;

    // ---- HUD ----
    public static string TagLine => IsChinese ? "/// 超充模式" : "/// SUPER CHARGE MODE";
    public static string TitleMode => IsChinese ? "超充模式" : "Super Charge Mode";
    public static string TagLineSaver => IsChinese ? "/// 省电模式" : "/// POWER SAVING MODE";
    public static string TitleSaver => IsChinese ? "省电模式" : "Power Saving Mode";

    // ---- 托盘菜单 ----
    public static string PreviewHud => IsChinese ? "预览电量 HUD" : "Preview Power HUD";
    public static string AutoStart => IsChinese ? "开机自启" : "Auto Start";
    public static string Settings => IsChinese ? "设置" : "Settings";
    public static string CheckUpdate => IsChinese ? "检查更新" : "Check for Updates";
    public static string Exit => IsChinese ? "退出" : "Exit";
    public static string Show => IsChinese ? "显示" : "Show";
    public static string About => IsChinese ? "关于" : "About";
    public static string TrayTooltip => IsChinese ? "EndfieldCharge · 电量 HUD" : "EndfieldCharge · Power HUD";

    // ---- 岛右键菜单 ----
    public static string Topmost => IsChinese ? "窗口置顶" : "Always on Top";
    public static string Plugins => IsChinese ? "插件" : "Plugins";
    /// <summary>右键 / 托盘菜单里「插件 ▸」子菜单的标题（内容实为面板跳转，故与设置页的「插件」分开）。</summary>
    public static string Panels => IsChinese ? "面板" : "Panels";
    public static string PluginBattery => IsChinese ? "电池仪表" : "Battery Gauge";
    public static string PluginFileTray => IsChinese ? "文件托盘" : "File Tray";
    public static string PluginWeather => IsChinese ? "天气" : "Weather";
    public static string MediaPrev => IsChinese ? "上一曲" : "Previous Track";
    public static string MediaNext => IsChinese ? "下一曲" : "Next Track";
    public static string MediaPause => IsChinese ? "暂停" : "Pause";
    public static string MediaPlay => IsChinese ? "播放" : "Play";
    public static string MediaUnavailable => IsChinese ? "未连接 SMTC" : "SMTC not connected";
    public static string DescTopmost => IsChinese ? "HUD 始终显示在其他窗口上方" : "Keep HUD above other windows";

    // ---- 设置窗口 ----
    public static string SettingsTitle => IsChinese ? "设置" : "Settings";
    public static string TabGeneral => IsChinese ? "通用" : "General";
    public static string TabNotifications => IsChinese ? "通知" : "Notifications";
    public static string TabAbout => IsChinese ? "关于" : "About";
    public static string TabDeveloper => IsChinese ? "开发者" : "Developer";
    public static string DeveloperComingSoon => IsChinese ? "开发者选项即将提供" : "Developer options coming soon";
    public static string LabelScale => IsChinese ? "全局缩放" : "Global Scale";
    /// <summary>抽屉头部品牌字标（中英一致，不随语言变化）。</summary>
    public static string Wordmark => "OVER THE FRONTIER | INTO THE FRONT";
    public static string BtnCollapse => IsChinese ? "收起" : "Collapse";

    // ---- 免打扰（整岛生效，右键菜单「免打扰 ▸」） ----
    public static string Dnd => IsChinese ? "免打扰" : "Do Not Disturb";
    public static string DndOff => IsChinese ? "取消免打扰" : "Turn Off";
    public static string Dnd5Min => IsChinese ? "5 分钟" : "5 minutes";
    public static string Dnd15Min => IsChinese ? "15 分钟" : "15 minutes";
    public static string Dnd1Hour => IsChinese ? "1 小时" : "1 hour";
    public static string Dnd3Hour => IsChinese ? "3 小时" : "3 hours";
    public static string DndPause => IsChinese ? "暂停（直到手动取消）" : "Pause (until turned off)";
    public static string DndPaused => IsChinese ? "已暂停" : "paused";
    public static string DndRemainingMinutes(int minutes) => IsChinese ? $"剩余 {minutes} 分钟" : $"{minutes} min left";
    public static string DndRemainingHours(int hours, int minutes) =>
        IsChinese ? $"剩余 {hours} 小时 {minutes} 分" : $"{hours} h {minutes} min left";

    // ---- 音乐插件设置（面板由插件自绘，字符串仍沿用同一张表） ----
    public static string MusicSectionTitle => IsChinese ? "音乐岛" : "Music Island";
    public static string LabelExpandedTimeout => IsChinese ? "展开态超时（秒，最低 3）" : "Expanded Timeout (s, min 3)";
    public static string LabelShowTitleWhenNoLyric => IsChinese ? "无歌词时显示歌名" : "Show Title When No Lyrics";
    public static string LabelShowVisualizer => IsChinese ? "显示可视化器" : "Show Visualizer";
    public static string LabelVisualizerIntensity => IsChinese ? "可视化强度（起伏幅度）" : "Visualizer Intensity";
    public static string LabelVisualizerBars => IsChinese ? "采样柱数（展开态频谱）" : "Spectrum Bars";
    public static string LabelLyricSource => IsChinese ? "歌词来源" : "Lyrics Source";
    public static string LyricSourceMerge =>
        IsChinese ? "并行择优（三源同时检索，取最匹配）" : "Best match (all sources in parallel)";
    public static string LyricSourcePreferLrclib => IsChinese ? "偏好 LRCLIB" : "Prefer LRCLIB";
    public static string LyricSourcePreferNetease => IsChinese ? "偏好网易云" : "Prefer NetEase";
    public static string LyricSourcePreferLocal => IsChinese ? "偏好本地" : "Prefer Local";
    public static string LyricSourceLrclib => IsChinese ? "仅 LRCLIB" : "LRCLIB only";
    public static string LyricSourceNetease => IsChinese ? "仅网易云" : "NetEase only";
    public static string LyricSourceLocal => IsChinese ? "仅本地" : "Local only";
    public static string LyricSourceOff => IsChinese ? "关闭歌词" : "Lyrics Off";
    public static string LabelMusicSourceWhitelist => IsChinese ? "音乐来源白名单" : "Music Source Whitelist";

    public static string HintMusicSourceWhitelist => IsChinese
        ? "留空 = 不采集频谱、也不自动弹岛"
        : "Empty = no capture, no auto-appear";
    public static string WhitelistAllowed => IsChinese ? "已允许" : "Allowed";
    public static string WhitelistEmpty => IsChinese ? "（空）" : "(empty)";
    public static string WhitelistPlaceholder => IsChinese ? "进程名 / AUMID" : "Process / AUMID";
    public static string WhitelistAdd => IsChinese ? "添加" : "Add";
    public static string WhitelistKnown => IsChinese ? "见过的来源" : "Seen sources";
    public static string WhitelistKnownEmpty => IsChinese ? "（暂无，播放一次即出现）" : "(none yet)";

    // ---- 模板插件设置（plugins/EndfieldCharge.Plugin.Template；同样是插件自绘面板） ----
    public static string PluginTemplateName => IsChinese ? "模板" : "Template";
    public static string TemplateSectionTitle => IsChinese ? "模板插件示例" : "Template Plugin Example";
    public static string TemplateLabelFlag => IsChinese ? "示例开关" : "Example Toggle";
    public static string TemplateDescFlag => IsChinese ? "演示 bool 设置：改动即保存" : "Bool setting: saved on change";
    public static string TemplateLabelNumber => IsChinese ? "示例数值" : "Example Number";
    public static string TemplateDescNumber =>
        IsChinese ? "演示数值设置：滑块与步进器双向同步" : "Numeric setting: slider and stepper stay in sync";
    public static string TemplateLabelChoice => IsChinese ? "示例选项" : "Example Choice";
    public static string TemplateDescChoice => IsChinese ? "演示枚举设置" : "Choice setting";
    public static string TemplateLabelText => IsChinese ? "示例文本" : "Example Text";
    public static string TemplateDescText => IsChinese ? "演示文本设置" : "Text setting";
    public static string TemplateChoiceAlpha => IsChinese ? "选项 A" : "Choice A";
    public static string TemplateChoiceBeta => IsChinese ? "选项 B" : "Choice B";
    public static string TemplateChoiceGamma => IsChinese ? "选项 C" : "Choice C";
    public static string TemplateTextPlaceholder => IsChinese ? "输入任意文本" : "Type anything";

    public static string NoPluginSettings => IsChinese ? "暂无插件提供设置" : "No plugin settings available";
    public static string LabelWaitingTimeout => IsChinese ? "等待态超时（秒）" : "Waiting Timeout (s)";
    public static string LabelContractedTimeout => IsChinese ? "收缩态超时（秒）" : "Contracted Timeout (s)";
    public static string LabelPosition => IsChinese ? "HUD 位置" : "HUD Position";
    public static string LabelMonitor => IsChinese ? "显示器" : "Monitor";
    public static string LabelWheelSwitch => IsChinese ? "滚轮切换岛" : "Wheel Island Switching";
    public static string DescWheelSwitch => IsChinese
        ? "鼠标滚轮滚动时，如何切换灵动岛"
        : "How scrolling the mouse wheel switches islands";
    public static string WheelSwitchWrap => IsChinese ? "循环切换" : "Cycle Through";
    public static string WheelSwitchClamp => IsChinese ? "到边界即停" : "Stop at Ends";
    public static string WheelSwitchDisabled => IsChinese ? "禁用" : "Disabled";
    public static string LabelLanguage => IsChinese ? "语言" : "Language";
    public static string ValueAuto => IsChinese ? "自动" : "Auto";
    public static string ValueChinese => IsChinese ? "中文" : "Chinese";
    public static string ValueEnglish => IsChinese ? "英文" : "English";
    public static string PosTopCenter => IsChinese ? "顶部居中" : "Top Center";
    public static string PosTopRight => IsChinese ? "顶部靠右" : "Top Right";
    public static string PosTopLeft => IsChinese ? "顶部靠左" : "Top Left";
    public static string LabelLowBattery => IsChinese ? "低电量提醒阈值" : "Low Battery Alert Threshold";
    public static string LabelLowBatteryEnable => IsChinese ? "启用低电量提醒" : "Enable Low Battery Alert";
    public static string LabelFullChargeEnable => IsChinese ? "充满时提醒" : "Alert When Fully Charged";// ---- 设置窗口段落标题 ----
    public static string SectionDisplay => IsChinese ? "显示" : "Display";
    public static string SectionPosition => IsChinese ? "位置与语言" : "Position & Language";
    public static string SectionStartup => IsChinese ? "启动" : "Startup";
    public static string SectionAlertSettings => IsChinese ? "提醒设置" : "Alert Settings";
    public static string DescAutoStart => IsChinese ? "登录 Windows 时自动启动" : "Auto start on Windows login";
    public static string DescLowBatteryAlert => IsChinese ? "电量低于阈值时弹窗提醒" : "Alert when battery drops below threshold";
    public static string DescFullChargeAlert => IsChinese ? "电池充满后弹窗通知" : "Notify when battery is fully charged";

    // ---- 关于 ----
    public static string LabelVersion => IsChinese ? "版本" : "Version";
    public static string LabelAuthor => IsChinese ? "作者" : "Author";
    public static string AboutSubtitle => IsChinese ? "终末地风格电量 HUD" : "Endfield-style Power HUD";

    // ---- 显示器 ----
    public static string MonitorName(int index, bool isPrimary) => IsChinese
        ? isPrimary ? $"显示器 {index + 1}（主）" : $"显示器 {index + 1}"
        : isPrimary ? $"Monitor {index + 1} (Primary)" : $"Monitor {index + 1}";
    public static string MonitorPrimaryDefault => IsChinese ? "主显示器（默认）" : "Primary Monitor (Default)";

    // ---- 动画页 ----
    public static string TabAnimation => IsChinese ? "动画" : "Animation";
    public static string SectionAnimParams => IsChinese ? "动画参数" : "Animation Parameters";
    public static string SectionPreview => IsChinese ? "预览" : "Preview";
    public static string LabelBounce => IsChinese ? "回弹强度" : "Bounce Strength";
    public static string LabelRippleIntensity => IsChinese ? "波纹强度" : "Ripple Intensity";
    public static string LabelRippleSpread => IsChinese ? "波纹幅度" : "Ripple Spread";
    public static string LabelPlayMode => IsChinese ? "播放模式" : "Play Mode";
    public static string ModePlug => IsChinese ? "插电（完整三态）" : "Plug In (Full Sequence)";
    public static string ModeUnplug => IsChinese ? "拔电（简化胶囊）" : "Unplug (Simple Capsule)";
    public static string ModeSaver => IsChinese ? "省电模式（完整三态）" : "Battery Saver (Full Sequence)";

    // ---- 通知 ----
    public static string LabelPowerSaverNotify => IsChinese ? "省电模式切换提示" : "Battery Saver Toggle Notify";
    public static string PowerSaverNotifyDesc => IsChinese
        ? "开启 / 关闭系统省电模式时显示 HUD"
        : "Show HUD when battery saver is toggled";
    public static string LabelUpdateCheck => IsChinese ? "自动检查更新" : "Auto Check for Updates";
    public static string LabelAutoStart => IsChinese ? "开机自启" : "Auto Start";
    public static string BtnCheckUpdate => IsChinese ? "检查更新" : "Check Now";
    public static string BtnSave => IsChinese ? "保存" : "Save";
    public static string SavedToast => IsChinese ? "设置已保存" : "Settings saved";

    // ---- 提醒 ----
    public static string LowBatteryTitle => IsChinese ? "电量不足" : "Low Battery";
    public static string LowBatteryMsg(int pct) => IsChinese
        ? $"电量仅剩 {pct}%，请及时充电"
        : $"Battery at {pct}%, please charge soon";
    public static string FullChargeTitle => IsChinese ? "已充满" : "Fully Charged";
    public static string FullChargeMsg => IsChinese ? "电池已充满，可以拔掉电源了" : "Battery is fully charged, you can unplug now";

    // ---- 更新 ----
    public static string UpdateTitle => IsChinese ? "发现新版本" : "Update Available";
    public static string UpdateMsg(string ver) => IsChinese
        ? $"EndfieldCharge {ver} 已发布，是否前往下载？"
        : $"EndfieldCharge {ver} is available. Download now?";
    public static string UpToDate => IsChinese ? "已是最新版本" : "You're up to date";
    public static string UpdateCheckFailed => IsChinese ? "检查更新失败" : "Update check failed";
    public static string BtnDownload => IsChinese ? "下载" : "Download";
    public static string BtnCancel => IsChinese ? "取消" : "Cancel";

    // ---- 预览 ----
    public static string PreviewTitle => IsChinese ? "动画预览" : "Animation Preview";
    public static string BtnPlay => IsChinese ? "播放" : "Play";

    // ---- 字体 ----
    public static string FontSectionTitle => IsChinese ? "字体" : "Font";
    public static string FontDesc => IsChinese
        ? "HUD 数字与英文字体使用 Inter Medium，可获得更清晰的渲染效果"
        : "HUD uses Inter Medium for digital and English text for sharper rendering";
    public static string BtnInstallFont => IsChinese ? "安装 Inter 字体" : "Install Inter Font";
    public static string FontInstalling => IsChinese ? "正在打开字体下载页…" : "Opening font download page...";
    public static string FontInstalled => IsChinese ? "下载后双击 Inter.ttf 文件即可安装" : "Download and double-click Inter.ttf to install";
}
