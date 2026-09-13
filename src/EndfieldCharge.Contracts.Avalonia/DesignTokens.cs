using Avalonia.Media;

namespace EndfieldCharge.Host.Island.Animation;

/// <summary>
/// 灵动岛「设计语言」共享 token：颜色 / 度量 / 时长。
///
/// 各插件皮肤（电池元插件 / 音乐 / …）统一从这里取值，保证 <b>视觉与动画语言一致</b>；
/// 皮肤各自的外观与布局仍由皮肤自己实现（本类不持有任何控件、不定义外观形状）。
/// 与 Styles/HudTheme.axaml 中的取色保持一致。
/// </summary>
public static class DesignTokens
{
    // ---------------- 颜色 ----------------
    /// <summary>胶囊背景（实心深炭色）。</summary>
    public static readonly Color Pill = Color.Parse("#312F30");

    /// <summary>白色圆底 / 方块图标底。</summary>
    public static readonly Color IconPlate = Color.Parse("#E9E7E4");

    /// <summary>图标深色。</summary>
    public static readonly Color BoltDark = Color.Parse("#141313");

    /// <summary>唯一强调色：黄绿。</summary>
    public static readonly Color Accent = Color.Parse("#C6CA4C");

    /// <summary>环/徽章暗盘底。</summary>
    public static readonly Color BadgeDark = Color.Parse("#262425");

    /// <summary>危险 / 低电量红。</summary>
    public static readonly Color Danger = Color.Parse("#FF4D4F");

    /// <summary>主文字（纯白，层级靠不透明度）。</summary>
    public static readonly Color TextPrimary = Color.Parse("#FFFFFF");

    public const double TextSecondaryOpacity = 0.55;
    public const double TextTertiaryOpacity = 0.40;

    // ---------------- 度量 ----------------
    /// <summary>胶囊基准宽度。</summary>
    public const double PillWidth = 560d;

    /// <summary>岛顶在窗口内的布局偏移（各皮肤共用，保证锚定一致）。</summary>
    public const double PillTop = 50d;

    /// <summary>圆胶囊高度（等待态）。</summary>
    public const double PillHeight = 60d;

    /// <summary>撑高后的高矩形高度（工业/展开态）。</summary>
    public const double PillHeightTall = 90d;

    /// <summary>收缩态宽度。</summary>
    public const double ContractedWidth = 200d;

    /// <summary>全圆角（圆胶囊）。</summary>
    public const double RadiusRound = 30d;

    /// <summary>矩形圆角。</summary>
    public const double RadiusRect = 18d;

    /// <summary>环直径。</summary>
    public const double RingDiameter = 46d;

    /// <summary>环描边厚度。</summary>
    public const double RingThickness = 4.5d;

    /// <summary>波纹抬升起点偏移（电标正下方）。</summary>
    public const double RippleRise = 16d;

    /// <summary>上滑入场 / 吸出退场的纵向位移。</summary>
    public const double SlideOffset = -110d;

    /// <summary>真实渲染全局缩放（HudTheme 的 GlobalScale）。</summary>
    public const double GlobalScale = 0.8d;

    // ---------------- 时长（共享过渡节奏） ----------------
    public static class Timing
    {
        /// <summary>胶囊宽度过渡（展开 / 收缩）。</summary>
        public const int WidthMs = 200;

        /// <summary>通用淡入淡出。</summary>
        public const int FadeMs = 200;

        /// <summary>揭示滑入。</summary>
        public const int SlideInMs = 260;

        /// <summary>退场（上滑 + 缩小 + 淡出）。</summary>
        public const int DismissMs = 220;

        /// <summary>退场时整体缩放终值。</summary>
        public const double DismissScale = 0.85d;
    }
}
