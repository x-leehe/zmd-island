using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using EndfieldCharge.Animations;
using EndfieldCharge.Contracts;

namespace EndfieldCharge.Host.Plugins.Battery;

/// <summary>
/// 电池元插件岛皮肤（UserControl）：
/// 视觉树 + 数据绑定（BindContent）+ 动画播放，全部由原 Views/HudWindow 原样搬移，
/// 视觉与动画行为保持不变。动画时间线工厂在 BatteryAnimationTheme（原 HudAnimations）。
/// </summary>
public partial class BatteryIslandView : UserControl
{
    private static readonly TimeSpan DismissDuration = TimeSpan.FromMilliseconds(220);

    private static readonly Color BadgeColorNormal = Color.Parse("#C6CA4C");
    private static readonly Color BadgeColorLow = Color.Parse("#FF4D4F");

    private const double RingDiameter = 46d;
    private const double RingThickness = 4.5d;

    private AnimationOptions _options = AnimationOptions.Default;
    private IslandContentDescriptor? _lastContent;

    /// <summary>岛内容当前可见（响应完成后 / 等待态 / 收缩态）。</summary>
    private bool _isPresent;

    /// <summary>当前处于收缩态（决定等待态入口走展开还是出现）。</summary>
    private bool _isContracted;

    /// <summary>指针进入 / 离开胶囊（宿主据此暂停 / 重置空闲计时）。</summary>
    public event Action? IslandPointerEntered;

    public event Action? IslandPointerExited;

    /// <summary>胶囊当前宽度（DIP）：点击穿透与悬停命中区随收缩态收窄。</summary>
    public double PillWidthDips => Pill.Width;

    public BatteryIslandView()
    {
        InitializeComponent();
        ApplyLocalization();
        ResetToInitial();

        Pill.PointerEntered += (_, _) => IslandPointerEntered?.Invoke();
        Pill.PointerExited += (_, _) => IslandPointerExited?.Invoke();
    }

    /// <summary>设置动画微调参数（宿主在 ApplySettings / 预览时调用）。</summary>
    public void SetAnimationOptions(AnimationOptions options) => _options = options;

    /// <summary>刷新本地化文案（充电 / 超充模式标题）。</summary>
    public void ApplyLocalization()
    {
        TagLineText.Text = Localization.TagLine;
        TitleText.Text = Localization.TitleMode;
    }

    /// <summary>应用全局缩放（设置项 GlobalScale）。</summary>
    public void ApplyScale(double globalScale)
    {
        GlobalScale.RenderTransform = new ScaleTransform(globalScale, globalScale);
    }

    /// <summary>调试用：直接呈现状态 C 静态画面（--debug-ring）。</summary>
    public void ShowStatic() => ShowFullyExpandedStatic();

    /// <summary>
    /// 布置揭示起点（隐藏 → 等待出现动画的初始帧）= 收缩态外观：
    /// 窄胶囊（200）+ 仅环/百分比可见 + 内容隐藏（无缩放弹出）。
    /// 宿主在 Show 窗口前调用——窗口首帧即收缩起点，不闪现整只胶囊。
    /// </summary>
    public void PrepareRevealStart()
    {
        SetSimpleCState(); // 状态 C 布局基础值（电标 -245、NumHost 就位、波纹隐藏）

        Pill.Width = 200d;
        PillShadow.Width = 200d;
        Pill.Opacity = 1d;
        PillShadow.Opacity = 1d;
        Pill.RenderTransform = new ScaleTransform(1d, 1d);
        PillShadow.RenderTransform = new ScaleTransform(1d, 1d);
        NumHost.Opacity = 0d;
        BoltIcon.Opacity = 0d;
        ContractedHost.IsVisible = true;
        ContractedHost.Opacity = 1d;

        // 揭示起点：整体位于上方（-110，与退场吸出的终点对称）——展开时滑回原位
        Root.RenderTransform = new TranslateTransform(0d, -110d);
    }

    // ---------------- 数据绑定 ----------------

    /// <summary>
    /// 将内容描述应用到视觉树（与旧 HudWindow.ApplyBattery 逐项等价）：
    /// 文案 / Wh / % / 徽章圆弧几何与配色。
    /// </summary>
    public void BindContent(IslandContentDescriptor content)
    {
        _lastContent = content;

        double fraction = Math.Clamp(content.RingFraction, 0d, 1d);

        WhValueText.Text = content.ValueText ?? "--";
        WhMaxText.Text = content.ValueUnit ?? string.Empty;
        PercentText.Text = content.PercentText ?? "--";

        if (content.Title is not null)
            TitleText.Text = content.Title;
        if (content.TagLine is not null)
            TagLineText.Text = content.TagLine;

        BadgeArc.Data = BuildRingGeometry(fraction, RingDiameter, RingThickness);

        var badgeColor = content.Tone == IslandTone.Danger
            ? BadgeColorLow
            : BadgeColorNormal;
        BadgeArc.Stroke = new SolidColorBrush(badgeColor);
        LaptopScreen.BorderBrush = new SolidColorBrush(badgeColor);
        LaptopBase.Background = new SolidColorBrush(badgeColor);
        BadgeElectrode.Background = new SolidColorBrush(badgeColor);

        // 收缩态环 + 百分比 + 笔记本图形/电极保持同步（与等待态 Badge 完全同构）
        ContractedArc.Data = BuildRingGeometry(fraction, RingDiameter, RingThickness);
        ContractedArc.Stroke = new SolidColorBrush(badgeColor);
        ContractedLaptopScreen.BorderBrush = new SolidColorBrush(badgeColor);
        ContractedLaptopBase.Background = new SolidColorBrush(badgeColor);
        ContractedElectrode.Background = new SolidColorBrush(badgeColor);
        ContractedPercentText.Text = content.PercentText is null ? "--" : content.PercentText + "%";
    }

    private static Geometry BuildRingGeometry(double fraction, double diameter, double thickness)
    {
        double radius = (diameter - thickness) / 2d;
        var center = new Point(diameter / 2d, diameter / 2d);

        double sweep = 360d * Math.Clamp(fraction, 0d, 1d);
        if (sweep < 0.5d) sweep = 0.5d;
        if (sweep > 359.5d) sweep = 359.5d;

        const double startAngle = -90d;
        var start = PointOnCircle(center, radius, startAngle);
        var end = PointOnCircle(center, radius, startAngle + sweep);

        var figure = new PathFigure { StartPoint = start, IsClosed = false };
        figure.Segments = new PathSegments
        {
            new ArcSegment
            {
                Point = end,
                Size = new Size(radius, radius),
                RotationAngle = 0d,
                IsLargeArc = sweep > 180d,
                SweepDirection = SweepDirection.Clockwise,
            },
        };

        return new PathGeometry { Figures = new PathFigures { figure } };
    }

    private static Point PointOnCircle(Point center, double radius, double degrees)
    {
        double rad = degrees * Math.PI / 180d;
        return new Point(center.X + radius * Math.Cos(rad), center.Y + radius * Math.Sin(rad));
    }

    // ---------------- 动画播放（皮肤契约，4 态生命周期） ----------------

    /// <summary>
    /// 响应动画（固定时长，结束于状态 C）：
    /// PlayKind.Full = 完整三态入场段（2.52s，不跑 ScaleOut / 波纹）；Simple = 简化胶囊入场段（0.4s）。
    /// 结束后固化状态 C 基础值并返回——不 Hide、不整体缩小。
    /// </summary>
    public async Task PlayResponseAsync(CancellationToken ct)
    {
        var o = _options;

        ResetToInitial();

        if (_lastContent?.PlayKind == IslandPlayKind.Simple)
        {
            SetSimpleCState();
            try
            {
                await Task.WhenAll(
                    HudAnimations.SimpleResponseIntro(o, HudAnimations.SimplePillAppear(o)).RunAsync(Pill, ct),
                    HudAnimations.SimpleResponseIntro(o, HudAnimations.SimplePillAppear(o)).RunAsync(PillShadow, ct),
                    HudAnimations.SimpleResponseIntro(o, HudAnimations.SimpleFadeIn(o)).RunAsync(BoltIcon, ct),
                    HudAnimations.SimpleResponseIntro(o, HudAnimations.SimpleFadeIn(o)).RunAsync(NumHost, ct));
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
        else
        {
            try
            {
                await Task.WhenAll(
                    HudAnimations.FullResponseIntro(o, HudAnimations.PillCorner(o)).RunAsync(Pill, ct),
                    HudAnimations.FullResponseIntro(o, HudAnimations.PillAppear(o)).RunAsync(Pill, ct),
                    HudAnimations.FullResponseIntro(o, HudAnimations.PillHeight(o)).RunAsync(Pill, ct),
                    HudAnimations.FullResponseIntro(o, HudAnimations.PillCorner(o)).RunAsync(PillShadow, ct),
                    HudAnimations.FullResponseIntro(o, HudAnimations.PillAppear(o)).RunAsync(PillShadow, ct),
                    HudAnimations.FullResponseIntro(o, HudAnimations.PillHeight(o)).RunAsync(PillShadow, ct),
                    HudAnimations.FullResponseIntro(o, HudAnimations.PillHeight(o)).RunAsync(RippleHost, ct),
                    HudAnimations.FullResponseIntro(o, HudAnimations.BoltIcon(o)).RunAsync(BoltIcon, ct),
                    HudAnimations.FullResponseIntro(o, HudAnimations.RippleHost(o)).RunAsync(RippleHost, ct),
                    HudAnimations.FullResponseIntro(o, HudAnimations.CircleForm(o)).RunAsync(CircleForm, ct),
                    HudAnimations.FullResponseIntro(o, HudAnimations.SquareForm(o)).RunAsync(SquareForm, ct),
                    HudAnimations.FullResponseIntro(o, HudAnimations.TitleHost(o)).RunAsync(TitleHost, ct),
                    HudAnimations.FullResponseIntro(o, HudAnimations.NumHost(o)).RunAsync(NumHost, ct));
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }

        // 响应结束：固化状态 C 基础值（动画时钟回收后仍保持状态 C）
        ShowFullyExpandedStatic();
        _isPresent = true;
        _isContracted = false;
    }

    /// <summary>
    /// 等待态入口（两条路径 + 保持）：
    ///   1. 收缩 → 等待 / 隐藏 → 等待：共用「收缩反向」展开动画——宽度 200→560 +
    ///      NumHost/BoltIcon 淡入、ContractedHost 淡出（200ms QuadraticEaseOut，无缩放弹出）。
    ///      隐藏态经 PrepareRevealStart 布置收缩起点并由宿主先 Show 窗口；
    ///      仅隐藏态额外叠加「从上方滑入」（Root Y -110→0，260ms ease-out）。
    ///   2. 响应完成 → 等待：已是状态 C，直接保持（无动画）。
    /// </summary>
    public async Task PlayWaitingAsync(CancellationToken ct)
    {
        if (_isPresent && !_isContracted)
            return; // 响应完成 → 等待：保持状态 C

        bool slideIn = !_isContracted; // 隐藏 → 等待：从上方滑入；收缩 → 等待：原地展开

        // 展开（收缩的反向）：先固化状态 C 基础值（宽度 560），动画只负责过渡
        ShowFullyExpandedStatic();
        ContractedHost.IsVisible = true;

        var tasks = new List<Task>
        {
            BuildWidthAnim(200d, 560d).RunAsync(Pill, ct),
            BuildWidthAnim(200d, 560d).RunAsync(PillShadow, ct),
            BuildFade(1d, 0d).RunAsync(ContractedHost, ct),
            BuildFade(0d, 1d).RunAsync(NumHost, ct),
            BuildFade(0d, 1d).RunAsync(BoltIcon, ct),
        };

        if (slideIn)
            tasks.Add(BuildSlideInAnim().RunAsync(Root, ct));

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        ContractedHost.IsVisible = false;
        ContractedHost.Opacity = 0d;

        // 滑入完成后把 Root 基础值复位到原位（动画时钟回收后不回跳）
        if (slideIn)
            Root.RenderTransform = new TranslateTransform(0d, 0d);

        _isContracted = false;
        _isPresent = true;
    }

    /// <summary>揭示滑入：整体从上方（-110）滑回原位，260ms ease-out——与宽度展开并行，稍长以平滑落定。</summary>
    private static Animation BuildSlideInAnim() => new()
    {
        Duration = TimeSpan.FromMilliseconds(260),
        FillMode = FillMode.Forward,
        Easing = new QuadraticEaseOut(),
        Children =
        {
            new KeyFrame { Cue = new Cue(0d), Setters = { new Setter(TranslateTransform.YProperty, -110d) } },
            new KeyFrame { Cue = new Cue(1d), Setters = { new Setter(TranslateTransform.YProperty, 0d) } },
        },
    };

    /// <summary>
    /// 收缩（等待 → 收缩）：胶囊宽度 560→200（Layoutable.WidthProperty 动画，无非均匀缩放），
    /// NumHost / 工业电标淡出、ContractedHost（环 + 百分比）淡入。
    /// 动画结束后固化收缩态基础值，保证状态持久。
    /// </summary>
    public async Task PlayContractAsync(CancellationToken ct)
    {
        ContractedHost.IsVisible = true;
        ContractedHost.Opacity = 0d;

        try
        {
            await Task.WhenAll(
                BuildWidthAnim(560d, 200d).RunAsync(Pill, ct),
                BuildWidthAnim(560d, 200d).RunAsync(PillShadow, ct),
                BuildFade(1d, 0d).RunAsync(NumHost, ct),
                BuildFade(1d, 0d).RunAsync(BoltIcon, ct),
                BuildFade(0d, 1d).RunAsync(ContractedHost, ct));
        }
        catch (OperationCanceledException)
        {
            return;
        }

        // 固化收缩态基础值（动画时钟回收后仍保持 200 宽、仅环 + 百分比）
        Pill.Width = 200d;
        PillShadow.Width = 200d;
        NumHost.Opacity = 0d;
        BoltIcon.Opacity = 0d;
        ContractedHost.Opacity = 1d;
        _isContracted = true;
        _isPresent = true;
    }

    /// <summary>
    /// 退场（→ 隐藏）：向上吸出屏幕 + 轻微缩小 + 淡出（220ms，加速 ease-in），
    /// 随后复位基础值（宽度回 560，阴影跟随复位）。
    /// </summary>
    public async Task PlayDismissAsync(CancellationToken ct)
    {
        var rise = new Animation
        {
            Duration = DismissDuration,
            FillMode = FillMode.Forward,
            Easing = new CubicEaseIn(),
            Children =
            {
                new KeyFrame { Cue = new Cue(0d), Setters = { new Setter(TranslateTransform.YProperty, 0d) } },
                new KeyFrame { Cue = new Cue(1d), Setters = { new Setter(TranslateTransform.YProperty, -110d) } },
            },
        };
        var shrink = new Animation
        {
            Duration = DismissDuration,
            FillMode = FillMode.Forward,
            Easing = new CubicEaseIn(),
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0d),
                    Setters =
                    {
                        new Setter(ScaleTransform.ScaleXProperty, 1d),
                        new Setter(ScaleTransform.ScaleYProperty, 1d),
                    },
                },
                new KeyFrame
                {
                    Cue = new Cue(1d),
                    Setters =
                    {
                        new Setter(ScaleTransform.ScaleXProperty, 0.85d),
                        new Setter(ScaleTransform.ScaleYProperty, 0.85d),
                    },
                },
            },
        };
        var fade = BuildFade(1d, 0d, (int)DismissDuration.TotalMilliseconds);

        try
        {
            await Task.WhenAll(
                rise.RunAsync(Root, ct),
                shrink.RunAsync(ScaleHost, ct),
                fade.RunAsync(Root, ct));
        }
        catch (OperationCanceledException)
        {
            return;
        }

        // 复位：隐藏后悬停命中区回到完整岛宽，下次出现从干净状态开始
        ResetToInitial();
        _isPresent = false;
        _isContracted = false;
    }

    // ---------------- 过渡辅助 ----------------

    private static Animation BuildWidthAnim(double from, double to) => new()
    {
        Duration = TimeSpan.FromMilliseconds(200),
        FillMode = FillMode.Forward,
        Easing = new QuadraticEaseOut(),
        Children =
        {
            new KeyFrame { Cue = new Cue(0d), Setters = { new Setter(Layoutable.WidthProperty, from) } },
            new KeyFrame { Cue = new Cue(1d), Setters = { new Setter(Layoutable.WidthProperty, to) } },
        },
    };

    private static Animation BuildFade(double from, double to, int ms = 200) => new()
    {
        Duration = TimeSpan.FromMilliseconds(ms),
        FillMode = FillMode.Forward,
        Easing = new QuadraticEaseOut(),
        Children =
        {
            new KeyFrame { Cue = new Cue(0d), Setters = { new Setter(OpacityProperty, from) } },
            new KeyFrame { Cue = new Cue(1d), Setters = { new Setter(OpacityProperty, to) } },
        },
    };

    // ---------------- 动画复位 ----------------

    private void ResetToInitial()
    {
        Root.Opacity = 1;
        Root.RenderTransform = new TranslateTransform(0d, 0d);

        ScaleHost.RenderTransform = new ScaleTransform(1d, 1d);

        Pill.Width = 560;
        Pill.Height = 60;
        Pill.CornerRadius = new CornerRadius(30d);
        Pill.Opacity = 0;
        Pill.RenderTransform = new ScaleTransform(0.6d, 0.6d);

        PillShadow.Width = 560;
        PillShadow.Height = 60;
        PillShadow.CornerRadius = new CornerRadius(30d);
        PillShadow.Opacity = 0;
        PillShadow.RenderTransform = new ScaleTransform(0.6d, 0.6d);

        RippleHost.RenderTransform = new TranslateTransform(0d, 0d);

        BoltIcon.RenderTransform = new TransformGroup
        {
            Children = { new ScaleTransform(0.4d, 0.4d), new TranslateTransform(0d, 0d) },
        };
        BoltIcon.Opacity = 0;

        RippleInnerHost.RenderTransform = new TranslateTransform(0d, 16d);
        RippleMidHost.RenderTransform = new TranslateTransform(0d, 16d);
        RippleOuterHost.RenderTransform = new TranslateTransform(0d, 16d);

        RippleInner.RenderTransform = new ScaleTransform(0d, 0d);
        RippleInner.Opacity = 0;
        RippleMid.RenderTransform = new ScaleTransform(0d, 0d);
        RippleMid.Opacity = 0;
        RippleOuter.RenderTransform = new ScaleTransform(0d, 0d);
        RippleOuter.Opacity = 0;

        CircleForm.Opacity = 0;
        SquareForm.Opacity = 0;

        TitleHost.RenderTransform = new TranslateTransform(0d, 0d);
        TitleHost.Opacity = 0;

        NumHost.RenderTransform = new TranslateTransform(0d, 0d);
        NumHost.Opacity = 0;

        ContractedHost.IsVisible = false;
        ContractedHost.Opacity = 0d;
    }

    private void ShowFullyExpandedStatic()
    {
        ScaleHost.RenderTransform = new ScaleTransform(1d, 1d);
        Pill.Width = 560;
        Pill.Height = 60;
        Pill.CornerRadius = new CornerRadius(30d);
        Pill.Opacity = 1;
        Pill.RenderTransform = new ScaleTransform(1d, 1d);

        PillShadow.Width = 560;
        PillShadow.Height = 60;
        PillShadow.CornerRadius = new CornerRadius(30d);
        PillShadow.Opacity = 1;
        PillShadow.RenderTransform = new ScaleTransform(1d, 1d);

        RippleHost.RenderTransform = new TranslateTransform(-245d, 0d);

        BoltIcon.RenderTransform = new TransformGroup
        {
            Children = { new ScaleTransform(1d, 1d), new TranslateTransform(-245d, 0d) },
        };
        BoltIcon.Opacity = 1;
        CircleForm.Opacity = 0;
        SquareForm.Opacity = 1;

        TitleHost.Opacity = 0;

        NumHost.RenderTransform = new TranslateTransform(0d, 0d);
        NumHost.Opacity = 1;
    }

    private void SetSimpleCState()
    {
        Pill.Width = 560;
        Pill.Height = 60;
        Pill.CornerRadius = new CornerRadius(30d);
        Pill.Opacity = 0;
        Pill.RenderTransform = new ScaleTransform(0.6d, 0.6d);

        PillShadow.Width = 560;
        PillShadow.Height = 60;
        PillShadow.CornerRadius = new CornerRadius(30d);
        PillShadow.Opacity = 0;
        PillShadow.RenderTransform = new ScaleTransform(0.6d, 0.6d);

        BoltIcon.RenderTransform = new TransformGroup
        {
            Children = { new ScaleTransform(1d, 1d), new TranslateTransform(-245d, 0d) },
        };
        BoltIcon.Opacity = 0;
        CircleForm.Opacity = 0;
        SquareForm.Opacity = 1;

        TitleHost.Opacity = 0;
        RippleHost.Height = 60;
        RippleHost.RenderTransform = new TranslateTransform(0d, 0d);
        RippleInnerHost.RenderTransform = new TranslateTransform(0d, 16d);
        RippleMidHost.RenderTransform = new TranslateTransform(0d, 16d);
        RippleOuterHost.RenderTransform = new TranslateTransform(0d, 16d);
        RippleInner.Opacity = 0;
        RippleMid.Opacity = 0;
        RippleOuter.Opacity = 0;

        NumHost.RenderTransform = new TranslateTransform(0d, 0d);
        NumHost.Opacity = 0;
    }
}
