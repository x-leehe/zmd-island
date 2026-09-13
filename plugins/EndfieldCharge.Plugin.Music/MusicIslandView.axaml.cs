using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using EndfieldCharge.Host.Island;
using EndfieldCharge.Host.Island.Animation;

namespace EndfieldCharge.Host.Plugins.Music;

/// <summary>
/// 音乐插件岛皮肤（骨架）：三态视觉树 + 数据绑定 + 过渡动画。
/// 三态映射：等待态 / 展开态（= 宿主 Response 状态）/ 收缩态；
/// 过渡复用共享动画原语（<see cref="AnimationPrimitives"/>）与设计 token（<see cref="DesignTokens"/>）。
/// </summary>
public partial class MusicIslandView : UserControl
{
    private const double UnfoldHeight = 160d;
    private const double RingSize = 40d;

    private MusicFrame _frame = MusicFrame.Empty;
    private bool _expanded;
    private bool _contracted;
    private bool _revealPending;

    public MusicIslandView()
    {
        InitializeComponent();
        // Handled=true：阻止冒泡到 HudWindow 的「左键消失」处理（岛内按钮优先）
        WaitExpand.PointerPressed += (_, e) => { e.Handled = true; ExpandToggled?.Invoke(); };
        UnfoldCover.PointerPressed += (_, e) => { e.Handled = true; ExpandToggled?.Invoke(); };
        WaitRing.PointerPressed += (_, e) => { e.Handled = true; TogglePlayPause(); };
        UnfoldPlay.PointerPressed += (_, e) => { e.Handled = true; TogglePlayPause(); };
        UnfoldPrev.PointerPressed += (_, e) => { e.Handled = true; Step(-1); };
        UnfoldNext.PointerPressed += (_, e) => { e.Handled = true; Step(1); };
        ResetToInitial();
        ApplyFrame();
    }

    /// <summary>岛内「展开 / 收起」按钮被点击。</summary>
    public event Action? ExpandToggled;

    /// <summary>岛内播放/暂停、上一曲、下一曲被点击（宿主 / 音乐模块据此驱动播放器）。</summary>
    public event Action<MusicAction>? ActionRequested;

    // ---------------- 皮肤契约 ----------------

    public IslandMetrics CurrentMetrics =>
        _expanded ? new IslandMetrics(DesignTokens.PillWidth, UnfoldHeight, DesignTokens.PillTop, _contentScale)
        : _contracted ? new IslandMetrics(DesignTokens.ContractedWidth, DesignTokens.PillHeight, DesignTokens.PillTop, _contentScale)
        : new IslandMetrics(DesignTokens.PillWidth, DesignTokens.PillHeight, DesignTokens.PillTop, _contentScale);

    public IslandMetrics HoverMetrics =>
        new(DesignTokens.PillWidth, UnfoldHeight, DesignTokens.PillTop, _contentScale);

    /// <summary>内部渲染缩放（= 设置项 GlobalScale，默认 0.8）；供宿主换算命中区尺寸。</summary>
    private double _contentScale = 0.8d;

    public void ApplyScale(double globalScale)
    {
        _contentScale = globalScale;
        GlobalScale.RenderTransform = new ScaleTransform(globalScale, globalScale);
    }

    /// <summary>布置揭示起点（宿主在 Show 窗口前调用）：收缩外观 + 位于上方，展开时滑回。</summary>
    public void PrepareRevealStart()
    {
        ResetToInitial();
        Pill.Opacity = 1d;
        PillShadow.Opacity = 1d;
        WaitHost.IsVisible = true;
        WaitHost.Opacity = 0d;
        Root.RenderTransform = new TranslateTransform(0d, DesignTokens.SlideOffset);
        _revealPending = true;
    }

    // ---------------- 数据绑定 ----------------

    public void BindMusic(MusicFrame frame)
    {
        _frame = frame;
        ApplyFrame();
    }

    private void ApplyFrame()
    {        var f = _frame;

        WaitLyric.Text = f.LyricCurrent ?? string.Empty;

        UnfoldTitleText.Text = f.Title ?? "--";
        UnfoldArtistText.Text = f.Artist ?? string.Empty;
        UnfoldLyric.Text = f.LyricCurrent ?? string.Empty;
        UpdateProgress(f.Progress);
        // 播放/暂停字形：同一 24×24 网格内切换可见性（不换几何，避免 Viewbox 抖动）
        PlayGlyph.IsVisible = !f.IsPlaying;
        PauseGlyph.IsVisible = f.IsPlaying;

        // 封面：有缩略图则铺满，无则露出底下的音符占位字形
        WaitCoverImage.Source = f.Cover;
        UnfoldCoverImage.Source = f.Cover;
        ContractCoverImage.Source = f.Cover;

        ApplyBars(WaitBar1, WaitBar2, WaitBar3, f.Spectrum, 24d);
        ApplyBars(ContractBar1, ContractBar2, ContractBar3, f.Spectrum, 24d);

        SpectrumPath.Fill = BuildSpectrumBrush();
        SpectrumPath.Data = SmoothSpectrum(f.Spectrum, 40);
    }

    /// <summary>
    /// 只推进进度相关视觉（展开态进度条 + 两个圆环），供本地插值 tick 使用
    /// （不重建标题/封面/频谱，避免每 250ms 的全量重绑）。
    /// </summary>
    public void UpdateProgress(double progress)
    {
        progress = Math.Clamp(progress, 0d, 1d);
        UnfoldProgressFill.Width = progress * 200d;

        double stroke = RingSize * DesignTokens.RingThickness / DesignTokens.RingDiameter;
        WaitRingArc.Data = RingGeometry.Build(progress, RingSize, stroke);
        ContractRingArc.Data = RingGeometry.Build(progress, RingSize, stroke);
    }

    /// <summary>播放/暂停：只发出请求；真实状态由媒体源下一帧回填（避免本地状态与 SMTC 打架）。</summary>
    private void TogglePlayPause() => ActionRequested?.Invoke(MusicAction.PlayPause);

    /// <summary>上/下一曲：只发出请求，由媒体源回填新曲目。</summary>
    private void Step(int delta) => ActionRequested?.Invoke(delta < 0 ? MusicAction.Previous : MusicAction.Next);

    /// <summary>3 根「上下轴对称」柱：按值设高并垂直居中。</summary>
    private static void ApplyBars(Rectangle b1, Rectangle b2, Rectangle b3, IReadOnlyList<float>? data, double area)
    {
        var bars = new[] { b1, b2, b3 };
        for (int i = 0; i < bars.Length; i++)
        {
            double v = data is { Count: > 0 } ? data[Math.Min(i, data.Count - 1)] : 0.5d;
            v = Math.Clamp(v, 0.08d, 1d);
            double h = Math.Round(v * area);
            bars[i].Height = h;
            Canvas.SetTop(bars[i], Math.Round((area - h) / 2d));
        }
    }

    /// <summary>平滑频谱填充笔刷：强调色向下渐隐。</summary>
    private static IBrush BuildSpectrumBrush()
    {
        var a = DesignTokens.Accent;
        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0d, 0d, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0d, 1d, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(a, 0d),
                new GradientStop(Color.FromArgb(0x20, a.R, a.G, a.B), 1d),
            },
        };
    }

    /// <summary>平滑频谱几何（Catmull-Rom → 三次贝塞尔），坐标 0..100，配合 Stretch=Fill 铺满。</summary>
    private static Geometry SmoothSpectrum(IReadOnlyList<float>? data, int count)
    {
        var vals = new double[count];
        for (int i = 0; i < count; i++)
        {
            double v = data is { Count: > 0 } ? data[(int)((long)i * data.Count / count)] : 0.4d;
            vals[i] = Math.Clamp(v, 0.05d, 1d);
        }

        var pts = new List<Point>(count);
        for (int i = 0; i < count; i++)
        {
            double x = count == 1 ? 50d : (double)i / (count - 1) * 100d;
            pts.Add(new Point(x, 100d - vals[i] * 100d));
        }

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(pts[0], true);
            for (int i = 0; i < pts.Count - 1; i++)
            {
                var p0 = i > 0 ? pts[i - 1] : pts[i];
                var p1 = pts[i];
                var p2 = pts[i + 1];
                var p3 = i + 2 < pts.Count ? pts[i + 2] : p2;
                var c1 = new Point(p1.X + (p2.X - p0.X) / 6d, p1.Y + (p2.Y - p0.Y) / 6d);
                var c2 = new Point(p2.X - (p3.X - p1.X) / 6d, p2.Y - (p3.Y - p1.Y) / 6d);
                ctx.CubicBezierTo(c1, c2, p2);
            }
            ctx.LineTo(new Point(100d, 100d));
            ctx.LineTo(new Point(0d, 100d));
            ctx.EndFigure(true);
        }
        return geometry;
    }

    // ---------------- 三态动画 ----------------

    /// <summary>展开态（复用宿主 Response）：高度 60→160、宽度→560，等待 ↔ 展开交叉淡化。</summary>
    public async Task PlayResponseAsync(CancellationToken ct)
    {
        bool fromContract = _contracted;
        _expanded = true;
        _contracted = false;

        WaitHost.IsVisible = true;
        UnfoldHost.IsVisible = true;
        ContractHost.IsVisible = fromContract;

        var tasks = new List<Task>
        {
            AnimatePill(DesignTokens.PillWidth, UnfoldHeight, ct),
            AnimationPrimitives.Fade(WaitHost.Opacity, 0d).RunAsync(WaitHost, ct),
            AnimationPrimitives.Fade(UnfoldHost.Opacity, 1d).RunAsync(UnfoldHost, ct),
            AnimationPrimitives.Fade(SpectrumPath.Opacity, 0.2d).RunAsync(SpectrumPath, ct),
        };
        if (fromContract)
            tasks.Add(AnimationPrimitives.Fade(ContractHost.Opacity, 0d).RunAsync(ContractHost, ct));

        // 自隐藏进入时布置了上方位移起点：展开路径同样要把它滑回 0，否则停在偏移处
        if (_revealPending)
        {
            _revealPending = false;
            tasks.Add(AnimationPrimitives.SlideY(DesignTokens.SlideOffset, 0d, DesignTokens.Timing.SlideInMs).RunAsync(Root, ct));
        }

        try { await Task.WhenAll(tasks); }
        catch (OperationCanceledException) { return; }

        Pill.Width = DesignTokens.PillWidth;
        PillShadow.Width = DesignTokens.PillWidth;
        Pill.Height = UnfoldHeight;
        PillShadow.Height = UnfoldHeight;
        Pill.Opacity = 1d;
        PillShadow.Opacity = 1d;
        WaitHost.IsVisible = false;
        WaitHost.Opacity = 0d;
        ContractHost.IsVisible = false;
        ContractHost.Opacity = 0d;
        UnfoldHost.Opacity = 1d;
        SpectrumPath.Opacity = 0.2d;
    }

    /// <summary>等待态：高度 / 宽度回 560×60，交叉淡化到等待内容；自隐藏进入时叠加下滑入场。</summary>
    public async Task PlayWaitingAsync(CancellationToken ct)
    {
        bool fromUnfold = _expanded;
        bool fromContract = _contracted;
        _expanded = false;
        _contracted = false;

        WaitHost.IsVisible = true;
        UnfoldHost.IsVisible = fromUnfold;
        ContractHost.IsVisible = fromContract;
        Pill.Opacity = 1d;
        PillShadow.Opacity = 1d;

        var tasks = new List<Task>
        {
            AnimatePill(DesignTokens.PillWidth, DesignTokens.PillHeight, ct),
            AnimationPrimitives.Fade(WaitHost.Opacity, 1d).RunAsync(WaitHost, ct),
        };
        if (fromUnfold)
        {
            tasks.Add(AnimationPrimitives.Fade(UnfoldHost.Opacity, 0d).RunAsync(UnfoldHost, ct));
            tasks.Add(AnimationPrimitives.Fade(SpectrumPath.Opacity, 0d).RunAsync(SpectrumPath, ct));
        }
        if (fromContract)
            tasks.Add(AnimationPrimitives.Fade(ContractHost.Opacity, 0d).RunAsync(ContractHost, ct));
        if (_revealPending)
        {
            _revealPending = false;
            tasks.Add(AnimationPrimitives.SlideY(DesignTokens.SlideOffset, 0d, DesignTokens.Timing.SlideInMs).RunAsync(Root, ct));
        }

        try { await Task.WhenAll(tasks); }
        catch (OperationCanceledException) { return; }

        Pill.Width = DesignTokens.PillWidth;
        PillShadow.Width = DesignTokens.PillWidth;
        Pill.Height = DesignTokens.PillHeight;
        PillShadow.Height = DesignTokens.PillHeight;
        WaitHost.Opacity = 1d;
        UnfoldHost.IsVisible = false;
        UnfoldHost.Opacity = 0d;
        SpectrumPath.Opacity = 0d;
        ContractHost.IsVisible = false;
        ContractHost.Opacity = 0d;
        Root.RenderTransform = new TranslateTransform(0d, 0d);
    }

    /// <summary>收缩态：宽度 560→200、内容交叉淡化到 环 + 3 柱。</summary>
    public async Task PlayContractAsync(CancellationToken ct)
    {
        _expanded = false;
        _contracted = true;

        ContractHost.IsVisible = true;
        WaitHost.IsVisible = true;
        try
        {
            await Task.WhenAll(
                AnimatePill(DesignTokens.ContractedWidth, DesignTokens.PillHeight, ct),
                AnimationPrimitives.Fade(WaitHost.Opacity, 0d).RunAsync(WaitHost, ct),
                AnimationPrimitives.Fade(ContractHost.Opacity, 1d).RunAsync(ContractHost, ct));
        }
        catch (OperationCanceledException) { return; }

        Pill.Width = DesignTokens.ContractedWidth;
        PillShadow.Width = DesignTokens.ContractedWidth;
        Pill.Height = DesignTokens.PillHeight;
        PillShadow.Height = DesignTokens.PillHeight;
        WaitHost.IsVisible = false;
        WaitHost.Opacity = 0d;
        ContractHost.Opacity = 1d;
    }

    /// <summary>退场：上滑 + 缩小 + 淡出。</summary>
    public async Task PlayDismissAsync(CancellationToken ct)
    {
        try
        {
            await Task.WhenAll(
                AnimationPrimitives.SlideY(0d, DesignTokens.SlideOffset, DesignTokens.Timing.DismissMs, new CubicEaseIn()).RunAsync(Root, ct),
                AnimationPrimitives.ScaleUniform(1d, DesignTokens.Timing.DismissScale, DesignTokens.Timing.DismissMs, new CubicEaseIn()).RunAsync(ScaleHost, ct),
                AnimationPrimitives.Fade(1d, 0d, DesignTokens.Timing.DismissMs).RunAsync(Root, ct));
        }
        catch (OperationCanceledException) { return; }

        ResetToInitial();
    }

    // ---------------- 辅助 ----------------

    private Task AnimatePill(double width, double height, CancellationToken ct) => Task.WhenAll(
        AnimationPrimitives.WidthTransition(Pill.Width, width).RunAsync(Pill, ct),
        AnimationPrimitives.WidthTransition(PillShadow.Width, width).RunAsync(PillShadow, ct),
        AnimationPrimitives.Transition(Layoutable.HeightProperty, Pill.Height, height, DesignTokens.Timing.WidthMs).RunAsync(Pill, ct),
        AnimationPrimitives.Transition(Layoutable.HeightProperty, PillShadow.Height, height, DesignTokens.Timing.WidthMs).RunAsync(PillShadow, ct));

    private void ResetToInitial()
    {
        Root.Opacity = 1d;
        Root.RenderTransform = new TranslateTransform(0d, 0d);
        ScaleHost.RenderTransform = new ScaleTransform(1d, 1d);

        Pill.Width = DesignTokens.PillWidth;
        Pill.Height = DesignTokens.PillHeight;
        Pill.CornerRadius = new CornerRadius(DesignTokens.RadiusRound);
        Pill.Opacity = 0d;
        Pill.RenderTransform = new ScaleTransform(1d, 1d);

        PillShadow.Width = DesignTokens.PillWidth;
        PillShadow.Height = DesignTokens.PillHeight;
        PillShadow.CornerRadius = new CornerRadius(DesignTokens.RadiusRound);
        PillShadow.Opacity = 0d;
        PillShadow.RenderTransform = new ScaleTransform(1d, 1d);

        SpectrumPath.Opacity = 0d;

        WaitHost.IsVisible = true;
        WaitHost.Opacity = 0d;
        UnfoldHost.IsVisible = false;
        UnfoldHost.Opacity = 0d;
        ContractHost.IsVisible = false;
        ContractHost.Opacity = 0d;

        _expanded = false;
        _contracted = false;
        _revealPending = false;
    }
}
