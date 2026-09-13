using System;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace EndfieldCharge.Host.Island.Animation;

/// <summary>
/// 参数化动画原语：只按「属性 + 起止值 + 时长 + 缓动」生成 <see cref="Animation"/>，
/// <b>不绑定任何控件名</b>。电池 / 音乐等皮肤共用同一套缓动与时长语言。
///
/// 多段关键帧用 <see cref="KeyFrame"/> + <see cref="AnimationNew"/> 搭配逐段 <see cref="KeySpline"/>；
/// 单段过渡用 <see cref="Fade"/> / <see cref="SlideY"/> / <see cref="ScaleUniform"/> / <see cref="WidthTransition"/>。
/// </summary>
public static class AnimationPrimitives
{
    // ---------------- 缓动 ----------------
    public static readonly KeySpline EaseIn = new(0.42, 0, 1, 1);
    public static readonly KeySpline EaseOut = new(0, 0, 0.58, 1);
    public static readonly KeySpline EaseInOut = new(0.42, 0, 0.58, 1);

    /// <summary>easeInOutCubic：位移专用——两端慢中间快、不过冲。</summary>
    public static readonly KeySpline Smooth = new(0.65, 0, 0.35, 1);

    /// <summary>回弹曲线：过冲量由 strength 控制（0 = 无过冲的快出曲线）。</summary>
    public static KeySpline BackOut(double strength) => new(0.175, 0.885, 0.32, 1d + strength);

    // ---------------- Setter ----------------
    public static Setter Set(AvaloniaProperty property, object value) => new(property, value);
    public static Setter Opacity(double v) => Set(Visual.OpacityProperty, v);
    public static Setter TranslateX(double v) => Set(TranslateTransform.XProperty, v);
    public static Setter TranslateY(double v) => Set(TranslateTransform.YProperty, v);
    public static Setter ScaleX(double v) => Set(ScaleTransform.ScaleXProperty, v);
    public static Setter ScaleY(double v) => Set(ScaleTransform.ScaleYProperty, v);
    public static Setter Width(double v) => Set(Layoutable.WidthProperty, v);
    public static Setter Height(double v) => Set(Border.HeightProperty, v);
    public static Setter CornerRadius(double v) => Set(Border.CornerRadiusProperty, new CornerRadius(v));

    // ---------------- 关键帧 / 时间线 ----------------
    /// <summary>构造关键帧；ks 非空时作为该段的缓动（多段下 <c>Animation.Easing</c> 不生效）。</summary>
    public static KeyFrame KeyFrame(double cue, KeySpline? ks, params Setter[] setters)
    {
        var kf = new KeyFrame { Cue = new Cue(cue) };
        if (ks is not null)
            kf.KeySpline = ks;
        foreach (var s in setters)
            kf.Setters.Add(s);
        return kf;
    }

    /// <summary>新建时长受 3~10s 钳制的动画（多段时间线用）。</summary>
    public static Avalonia.Animation.Animation AnimationNew(double durationSeconds) => new()
    {
        Duration = TimeSpan.FromSeconds(Math.Clamp(durationSeconds, 3d, 10d)),
        FillMode = FillMode.Forward,
    };

    /// <summary>把基线 cue 映射到实际时间线（入场段固定占比，其余线性分配）。</summary>
    public static double MapCue(double cue, double introEndCue, double baselineSeconds, double durationSeconds)
    {
        double d = Math.Clamp(durationSeconds, 3d, 10d);
        double introFrac = introEndCue * baselineSeconds / d;
        if (cue <= introEndCue)
            return cue / introEndCue * introFrac;
        return introFrac + (cue - introEndCue) / (1 - introEndCue) * (1 - introFrac);
    }

    /// <summary>
    /// 裁剪动画为「入场段」：丢弃 cue &gt; endCue 的关键帧，剩余帧按 endCue 归一化（cue / endCue），
    /// 保持原 KeySpline——动画铺满整个 Duration。
    /// </summary>
    public static Avalonia.Animation.Animation Trim(Avalonia.Animation.Animation full, double endCue, double durationSeconds)
    {
        var intro = new Avalonia.Animation.Animation
        {
            Duration = TimeSpan.FromSeconds(durationSeconds),
            FillMode = FillMode.Forward,
        };

        foreach (var kf in full.Children)
        {
            if (kf.Cue.CueValue > endCue + 0.0001)
                continue;

            var copy = new KeyFrame
            {
                Cue = new Cue(kf.Cue.CueValue / endCue),
                KeySpline = kf.KeySpline,
            };
            foreach (var s in kf.Setters)
                copy.Setters.Add(s);
            intro.Children.Add(copy);
        }

        return intro;
    }

    // ---------------- 单段过渡 ----------------
    /// <summary>通用两帧过渡（默认 QuadraticEaseOut）。</summary>
    public static Avalonia.Animation.Animation Transition(AvaloniaProperty property, object from, object to,
        int ms = DesignTokens.Timing.FadeMs, Easing? easing = null) => new()
        {
            Duration = TimeSpan.FromMilliseconds(ms),
            FillMode = FillMode.Forward,
            Easing = easing ?? new QuadraticEaseOut(),
            Children =
        {
            new KeyFrame { Cue = new Cue(0d), Setters = { new Setter(property, from) } },
            new KeyFrame { Cue = new Cue(1d), Setters = { new Setter(property, to) } },
        },
        };

    /// <summary>淡入淡出（默认 200ms QuadraticEaseOut）。</summary>
    public static Avalonia.Animation.Animation Fade(double from, double to,
        int ms = DesignTokens.Timing.FadeMs, Easing? easing = null) =>
        Transition(Visual.OpacityProperty, from, to, ms, easing);

    /// <summary>纵向滑动。</summary>
    public static Avalonia.Animation.Animation SlideY(double from, double to,
        int ms, Easing? easing = null) =>
        Transition(TranslateTransform.YProperty, from, to, ms, easing);

    /// <summary>胶囊宽度过渡（默认 200ms QuadraticEaseOut）。</summary>
    public static Avalonia.Animation.Animation WidthTransition(double from, double to,
        int ms = DesignTokens.Timing.WidthMs, Easing? easing = null) =>
        Transition(Layoutable.WidthProperty, from, to, ms, easing);

    /// <summary>整体等比缩放（同时驱动 ScaleX / ScaleY）。</summary>
    public static Avalonia.Animation.Animation ScaleUniform(double from, double to,
        int ms, Easing? easing = null) => new()
        {
            Duration = TimeSpan.FromMilliseconds(ms),
            FillMode = FillMode.Forward,
            Easing = easing ?? new QuadraticEaseOut(),
            Children =
        {
            new KeyFrame { Cue = new Cue(0d), Setters = { ScaleX(from), ScaleY(from) } },
            new KeyFrame { Cue = new Cue(1d), Setters = { ScaleX(to), ScaleY(to) } },
        },
        };
}
