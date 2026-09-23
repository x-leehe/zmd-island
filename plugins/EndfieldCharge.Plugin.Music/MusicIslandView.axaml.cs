using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using EndfieldCharge.Host.Island;
using EndfieldCharge.Host.Island.Animation;
using EndfieldCharge.Services;

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

    /// <summary>一屏文字"快唱完了"的余量：唱到的位置逼近右缘这么多时，才 unfold 下一段。</summary>
    private const double FollowMargin = 0.12d;

    /// <summary>翻页补间时长（毫秒）：一次只滑一小段，所以短一点就够，也更"缓缓"。</summary>
    private const double FollowTweenMs = 420d;

    /// <summary>
    /// 一个歌词位：**定宽的框**（<see cref="Frame"/>，尺寸即可视窗口）+
    /// 可整体平移的「歌词层」（<see cref="Layer"/>）。
    /// <para>
    /// 移动的是**层**，歌词本体（<see cref="Text"/>）除了写入文本之外不做任何操作 ——
    /// 不给它 Width、不动它的 TextTrimming：它就该以完整、未改写的形态待在层里，
    /// 由框决定观众看到哪一段。
    /// </para>
    /// </summary>
    private sealed class LyricFollowTarget
    {
        public LyricFollowTarget(TextBlock text, Canvas layer)
        {
            Text = text;
            Layer = layer;
        }

        public TextBlock Text { get; }

        public Canvas Layer { get; }

        /// <summary>歌词层的自然宽度（每次换文本后重新量）。</summary>
        public double Natural { get; set; }

        /// <summary>当前窗口起点（文本比例 0..1）。</summary>
        public double PageStart { get; set; }

        /// <summary>正在跑的补间（翻页时取消上一段，避免两段动画抢同一个 X）。</summary>
        public CancellationTokenSource? Tween { get; set; }
    }

    private readonly LyricFollowTarget[] _lyrics;

    private MusicFrame _frame = MusicFrame.Empty;
    private string? _sourceIconId;
    private Bitmap? _sourceIcon;
    private bool _expanded;
    private bool _contracted;
    private bool _revealPending;
    private bool _showTitleWhenNoLyric = true;
    private bool _showVisualizer = true;
    private double _visualizerIntensity = 1.5d;
    private int _visualizerBars = 64;

    /// <summary>跑马灯动画的取消源（按歌词位分别记）：换句时先取消上一段，免得两段动画抢同一个 X。</summary>
    private readonly Dictionary<TextBlock, CancellationTokenSource> _marqueeTokens = new();

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
        // 两个歌词位：等待态 / 展开态。移动的是「歌词层」，歌词本体只写文本、不做别的。
        _lyrics = new[]
        {
            new LyricFollowTarget(WaitLyric, WaitLyricLayer),
            new LyricFollowTarget(UnfoldLyric, UnfoldLyricLayer),
        };

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
        if (string.Equals(_sourceIconId, frame.SourceAppId, StringComparison.OrdinalIgnoreCase))
            return;

        _sourceIconId = frame.SourceAppId;
        UnfoldSourceIcon.Source = null;
        UnfoldSourceBadge.IsVisible = false;
        if (string.IsNullOrWhiteSpace(frame.SourceAppId))
            return;

        string sourceId = frame.SourceAppId;
        _ = LoadSourceIconAsync(sourceId);
    }

    private async Task LoadSourceIconAsync(string sourceId)
    {
        var icon = await Task.Run(() => SourceAppIcon.TryLoad(sourceId));
        if (!string.Equals(_sourceIconId, sourceId, StringComparison.OrdinalIgnoreCase))
        {
            icon?.Dispose();
            return;
        }

        UnfoldSourceIcon.Source = icon;
        UnfoldSourceBadge.IsVisible = icon is not null;
        var old = _sourceIcon;
        _sourceIcon = icon;
        if (old is not null)
            Avalonia.Threading.Dispatcher.UIThread.Post(old.Dispose, Avalonia.Threading.DispatcherPriority.Background);
    }

    private void ApplyFrame()
    {
        var f = _frame;

        // 歌词**不在这里刷**：`MusicFrame.LyricCurrent` 对真实帧恒为 null（歌词由插件的
        // LyricsService 按播放位置推进来），每帧拿它去写歌词，会把真歌词反复清成空/曲名 ——
        // 表现成"翻页后又自己跑回句首"（换文本 → 窗口复位）。只有降级态那种**带文案**的帧才写。
        if (!string.IsNullOrEmpty(f.LyricCurrent))
            ApplyLyric(f.LyricCurrent);

        UnfoldTitleText.Text = f.Title ?? "--";
        UnfoldArtistText.Text = f.Artist ?? string.Empty;
        UpdateProgress(f.Progress);
        // 播放/暂停字形：同一 24×24 网格内切换可见性（不换几何，避免 Viewbox 抖动）
        PlayGlyph.IsVisible = !f.IsPlaying;
        PauseGlyph.IsVisible = f.IsPlaying;
        // 等待态的圆环中心按钮同样要反映真实播放态（此前写死在播放图标上）
        WaitRingIcon.IsVisible = !f.IsPlaying;
        WaitRingPauseIcon.IsVisible = f.IsPlaying;

        // 封面：有缩略图则铺满，无则露出底下的音符占位字形
        WaitCoverImage.Source = f.Cover;
        UnfoldCoverImage.Source = f.Cover;
        ContractCoverImage.Source = f.Cover;

        // 这里**不碰频谱**：`MusicFrame.Spectrum` 一直是 null（真实数据由 30fps 的 tick 推进来），
        // 每帧拿它刷视图只会把已经画好的背景层清成静止 —— "展开态背景频谱没跑起来"就是这个。
    }

    /// <summary>
    /// 展开态那层背景平滑频谱（**真实采集数据**）。只画背景层 ——
    /// 等待态 / 收缩态的三根柱走 <see cref="UpdatePlayIndicator"/>（假动画），两码事。
    /// </summary>
    public void UpdateSpectrum(IReadOnlyList<float>? spectrum)
    {
        SpectrumPath.Fill = BuildSpectrumBrush();
        SpectrumPath.Data = SmoothSpectrum(Amplify(spectrum), _visualizerBars);
    }

    /// <summary>
    /// 等待态 / 收缩态那三根柱：**不接真实采样**，只按相位做"在播放"的假动画
    /// （见 <see cref="PlayIndicator"/>）—— 柱子在跳 = 音乐在放，暂停即归零。
    /// </summary>
    public void UpdatePlayIndicator(bool playing, double phase)
    {
        float[] levels = Amplify(PlayIndicator.Build(playing, phase));
        ApplyBars(WaitBar1, WaitBar2, WaitBar3, levels, 24d);
        ApplyBars(ContractBar1, ContractBar2, ContractBar3, levels, 24d);
    }

    /// <summary>按设置的「起伏强度」对可视化数据做对比度扩展（以 0.5 为轴）。</summary>
    private float[] Amplify(IReadOnlyList<float>? data)
    {
        if (data is not { Count: > 0 })
            return Array.Empty<float>();

        var result = new float[data.Count];
        for (int i = 0; i < data.Count; i++)
            result[i] = (float)SpectrumCurve.Amplify(data[i], _visualizerIntensity);

        return result;
    }

    /// <summary>
    /// 把「当前歌词」写进两处歌词位：等待态那一行在歌词为空时按设置退回曲名，展开态则保持空。
    /// 歌词行由插件的 <c>LyricsService</c> 按播放位置算出后传进来。
    /// <para>
    /// 注意：本方法会被 250ms 的进度 tick **反复调用**（同一句歌词反复进来），
    /// 所以这里**绝不能**复位窗口 —— 复位只发生在 <see cref="ApplyLyricText"/> 真正换了文本时。
    /// 曾经在这里无条件复位，结果每 250ms 把窗口打回句首再被 60fps 推出去，滚成了一片"鬼畜"。
    /// </para>
    /// </summary>
    private void ApplyLyric(string? lyric)
    {
        var waitText = string.IsNullOrEmpty(lyric)
            ? (_showTitleWhenNoLyric ? _frame.Title ?? string.Empty : string.Empty)
            : lyric;

        ApplyLyricText(_lyrics[0], waitText);
        ApplyLyricText(_lyrics[1], lyric ?? string.Empty);
    }

    /// <summary>
    /// 换行时把文本**原样**写进歌词本体，重量一次自然宽度，并把窗口复位到句首。
    /// <para>
    /// 对歌词本体的唯一操作就是这一句 <c>.Text =</c>：不给 Width、不动 TextTrimming。
    /// 层里的 TextBlock 处在**不约束子元素**的 Canvas 中，量出来就是它真实的排版宽度，
    /// 不需要自己算 —— 早先自己算宽度并据此切省略号，那等于在**改写歌词**（也就是"被截断"的观感）。
    /// </para>
    /// </summary>
    private static void ApplyLyricText(LyricFollowTarget target, string text)
    {
        if (string.Equals(target.Text.Text, text, StringComparison.Ordinal))
            return;

        target.Text.Text = text;

        target.Tween?.Cancel();
        target.Tween = null;
        target.PageStart = 0d;

        target.Text.Measure(Size.Infinity);
        target.Natural = NaturalWidth(target.Text);

        if (target.Layer.RenderTransform is TranslateTransform layer)
            layer.X = 0d;
        else
            target.Layer.RenderTransform = new TranslateTransform();
    }

    /// <summary>
    /// 跟唱推进：**不是**匀速连续滚动，而是"这一屏快唱完了 → 缓缓 unfold 出后面一段"。
    /// <para>
    /// 因此文本绝大多数时间是**不动**的；且只允许向前翻页（<c>next &gt; PageStart</c> 才动），
    /// 播放位置的细微抖动不会造成来回横跳。
    /// </para>
    /// </summary>
    public void UpdateLyricFollow(double progress)
    {
        progress = Math.Clamp(progress, 0d, 1d);

        foreach (var target in _lyrics)
        {
            double box = target.Layer.Width;                    // 与框同宽（XAML 里成对写的）
            if (target.Natural <= 0d || box <= 0d || target.Natural <= box)
                continue;                                       // 放得下：这一位不跟唱

            double visible = box / target.Natural;               // 一屏能显示整句的几成
            if (!LyricPager.TryAdvance(target.PageStart, visible, progress, FollowMargin, out double next))
                continue;                                       // 还不到时候（"不应该一直尝试动"）

            target.PageStart = next;
            StartFollowTween(target, -next * target.Natural);
        }
    }

    /// <summary>把窗口缓缓滑到新位置（有限时长的补间；目标必须是可视元素，不能是 Transform）。</summary>
    private static void StartFollowTween(LyricFollowTarget target, double to)
    {
        if (target.Layer.RenderTransform is not TranslateTransform transform)
            return;

        target.Tween?.Cancel();

        double from = transform.X;
        if (Math.Abs(to - from) < 0.5d)
        {
            transform.X = to;
            return;
        }

        var cts = new CancellationTokenSource();
        target.Tween = cts;

        // ① 先把**基值**推到目标位。动画默认 FillMode=None：跑完（或被取消）会退回基值 ——
        //    基值还停在 0 的话，每一页都"滑过去 → 演完 → 自己飞回句首"（"念旧情结"的根因）。
        transform.X = to;

        var animation = new Animation
        {
            // ② 再让动画值在结束后保留（项目既有写法，见右键菜单的展开动画）
            FillMode = FillMode.Forward,
            Duration = TimeSpan.FromMilliseconds(FollowTweenMs),
            Children =
            {
                new KeyFrame { Cue = new Cue(0d), Setters = { new Setter(TranslateTransform.XProperty, from) } },
                new KeyFrame
                {
                    Cue = new Cue(1d),
                    // 多关键帧下 Animation.Easing 不生效，逐段显式给缓动（项目既有约定）
                    KeySpline = new KeySpline(0.4d, 0d, 0.2d, 1d),
                    Setters = { new Setter(TranslateTransform.XProperty, to) },
                },
            },
        };

        // 目标是**可视元素**（这一层 Canvas），不是 Transform 本身
        _ = ConfirmFollowAsync(target, animation, cts.Token, to);
    }

    /// <summary>
    /// 补间结束后核对一次：X 应当**停在目标位**。
    /// 动画若不留值（FillMode=None）跑完会退回基值 —— 表面看就是"翻页后自己飞回句首"。
    /// 这行日志让那个陷阱再也藏不住（期望与实际摆在一起）。
    /// </summary>
    private static async Task ConfirmFollowAsync(
        LyricFollowTarget target, Animation animation, CancellationToken token, double expected)
    {
        try
        {
            await animation.RunAsync(target.Layer, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        double actual = target.Layer.RenderTransform is TranslateTransform transform ? transform.X : double.NaN;
        bool ok = Math.Abs(actual - expected) < 0.5d;

        Logger.Info($"Music: 跟唱落位（期望 {expected:F1} · 实际 {actual:F1}）{(ok ? string.Empty : " ← 偏了：动画没留值")}");
    }

    /// <summary>量文本的**自然宽度**（层里不约束子元素，所以量出来就是真实排版宽度）。</summary>
    private static double NaturalWidth(TextBlock target)
    {
        target.Measure(Size.Infinity);
        return target.DesiredSize.Width;
    }
    /// <summary>只刷新歌词（换行点由本地插值 tick 驱动），不重建标题 / 封面 / 频谱。</summary>
    public void UpdateLyric(string? lyric) => ApplyLyric(lyric);

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

    /// <summary>应用插件设置里的视觉偏好（宿主设置窗口「插件 → 音乐」改动后立即回流）。</summary>
    public void ApplyPreferences(bool showTitleWhenNoLyric, bool showVisualizer, double visualizerIntensity, int visualizerBars)
    {
        _showTitleWhenNoLyric = showTitleWhenNoLyric;
        _showVisualizer = showVisualizer;
        _visualizerIntensity = visualizerIntensity;
        _visualizerBars = visualizerBars;

        SpectrumPath.IsVisible = showVisualizer;
        foreach (var bar in new[] { WaitBar1, WaitBar2, WaitBar3, ContractBar1, ContractBar2, ContractBar3 })
            bar.IsVisible = showVisualizer;

        ApplyFrame();
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

    /// <summary>
    /// 平滑频谱几何（Catmull-Rom → 三次贝塞尔）。坐标就是**像素**（560×160，与 SpectrumPath 同尺寸），
    /// 因此路径必须是 <c>Stretch="None"</c>：相对坐标 + <c>Stretch="Fill"</c> 会把包围盒拉伸铺满，
    /// 各段趋近 0 时残余会被重新拉满 —— 那就是"暂停后缓缓填充"的假象。
    /// <para>
    /// 建曲线前做两轮 [1,2,1] 邻域平滑（见 <see cref="SpectrumCurve.SmoothNeighbours"/>）：
    /// 相邻频段之间的跳变才是"突兀"的来源。<b>两轮足够</b> —— 三轮会把局部峰谷也抹掉。
    /// </para>
    /// </summary>
    private static Geometry SmoothSpectrum(IReadOnlyList<float>? data, int count)
    {
        const double width = DesignTokens.PillWidth;
        const double height = UnfoldHeight;

        var vals = new double[count];
        for (int i = 0; i < count; i++)
        {
            double v = data is { Count: > 0 } ? data[(int)((long)i * data.Count / count)] : 0d;
            vals[i] = Math.Clamp(v, 0d, 1d);
        }

        SpectrumCurve.SmoothNeighbours(vals, 2);

        var pts = new List<Point>(count);
        for (int i = 0; i < count; i++)
        {
            double x = count == 1 ? width / 2d : (double)i / (count - 1) * width;
            pts.Add(new Point(x, height - (vals[i] * height)));
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
            ctx.LineTo(new Point(width, height));
            ctx.LineTo(new Point(0d, height));
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
