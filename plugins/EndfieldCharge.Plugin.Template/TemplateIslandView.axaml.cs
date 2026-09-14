using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using EndfieldCharge.Host.Island;
using EndfieldCharge.Host.Island.Animation;

namespace EndfieldCharge.Host.Plugins.Template;

/// <summary>
/// 模板插件岛皮肤：三态**空壳**——视觉树里只有胶囊背景，没有任何文案 / 图标 / 仪表。
/// 它演示的是「尺寸与动画」的最小骨架：
///   等待态 560×60 · 展开（宿主 Response）560×160 · 收缩态 200×60 · 退场淡出；
/// 过渡统一走共享原语 <see cref="AnimationPrimitives"/> 与设计 token <see cref="DesignTokens"/>。
/// 第三方插件只需在 Pill 内加入自己的内容，其余生命周期交给宿主。
/// </summary>
public partial class TemplateIslandView : UserControl
{
    /// <summary>展开态（宿主 Response）的胶囊高度，与 WindowHeight 一致。</summary>
    private const double ExpandedHeight = 160d;

    /// <summary>内部渲染缩放（= 设置项 GlobalScale，默认 0.8）；供宿主换算命中区尺寸。</summary>
    private double _contentScale = 0.8d;

    private bool _expanded;
    private bool _contracted;
    private bool _revealPending;

    public TemplateIslandView()
    {
        InitializeComponent();
        ResetToInitial();
    }

    // ---------------- 皮肤契约 ----------------

    /// <summary>当前岛尺寸：展开 560×160 / 等待 560×60 / 收缩 200×60。</summary>
    public IslandMetrics CurrentMetrics =>
        _expanded ? new IslandMetrics(DesignTokens.PillWidth, ExpandedHeight, DesignTokens.PillTop, _contentScale)
        : _contracted ? new IslandMetrics(DesignTokens.ContractedWidth, DesignTokens.PillHeight, DesignTokens.PillTop, _contentScale)
        : new IslandMetrics(DesignTokens.PillWidth, DesignTokens.PillHeight, DesignTokens.PillTop, _contentScale);

    /// <summary>完整岛区（悬停唤醒区不随收缩收窄）。</summary>
    public IslandMetrics HoverMetrics =>
        new(DesignTokens.PillWidth, ExpandedHeight, DesignTokens.PillTop, _contentScale);

    /// <summary>应用全局缩放（设置项 GlobalScale）。</summary>
    public void ApplyScale(double globalScale)
    {
        _contentScale = globalScale;
        GlobalScale.RenderTransform = new ScaleTransform(globalScale, globalScale);
    }

    /// <summary>
    /// 布置揭示起点（宿主在 Show 窗口前调用）：胶囊从收缩宽度 + 上方位移开始，
    /// 避免窗口首帧闪现整只胶囊；随后的等待 / 展开动画沿同一方向滑回原位。
    /// </summary>
    public void PrepareRevealStart()
    {
        ResetToInitial();
        Pill.Opacity = 1d;
        PillShadow.Opacity = 1d;
        Pill.Width = DesignTokens.ContractedWidth;
        PillShadow.Width = DesignTokens.ContractedWidth;
        Root.RenderTransform = new TranslateTransform(0d, DesignTokens.SlideOffset);
        _contracted = true;
        _revealPending = true;
    }

    // ---------------- 三态动画 ----------------

    /// <summary>响应态 / 展开态：胶囊长高到 560×160 并停住（空壳没有内容需要交叉淡化）。</summary>
    public async Task PlayResponseAsync(CancellationToken ct)
    {
        _expanded = true;
        _contracted = false;
        Pill.Opacity = 1d;
        PillShadow.Opacity = 1d;

        var tasks = new List<Task>
        {
            AnimatePill(DesignTokens.PillWidth, ExpandedHeight, ct),
        };
        if (_revealPending)
        {
            _revealPending = false;
            tasks.Add(AnimationPrimitives.SlideY(DesignTokens.SlideOffset, 0d, DesignTokens.Timing.SlideInMs).RunAsync(Root, ct));
        }

        try { await Task.WhenAll(tasks); }
        catch (OperationCanceledException) { return; }

        ApplyFinalFrame(DesignTokens.PillWidth, ExpandedHeight);
    }

    /// <summary>等待态：胶囊回到 560×60；自隐藏进入时叠加下滑入场。</summary>
    public async Task PlayWaitingAsync(CancellationToken ct)
    {
        _expanded = false;
        _contracted = false;
        Pill.Opacity = 1d;
        PillShadow.Opacity = 1d;

        var tasks = new List<Task>
        {
            AnimatePill(DesignTokens.PillWidth, DesignTokens.PillHeight, ct),
        };
        if (_revealPending)
        {
            _revealPending = false;
            tasks.Add(AnimationPrimitives.SlideY(DesignTokens.SlideOffset, 0d, DesignTokens.Timing.SlideInMs).RunAsync(Root, ct));
        }

        try { await Task.WhenAll(tasks); }
        catch (OperationCanceledException) { return; }

        ApplyFinalFrame(DesignTokens.PillWidth, DesignTokens.PillHeight);
        Root.RenderTransform = new TranslateTransform(0d, 0d);
    }

    /// <summary>收缩态：胶囊收窄到 200×60。</summary>
    public async Task PlayContractAsync(CancellationToken ct)
    {
        _expanded = false;
        _contracted = true;

        try { await AnimatePill(DesignTokens.ContractedWidth, DesignTokens.PillHeight, ct); }
        catch (OperationCanceledException) { return; }

        ApplyFinalFrame(DesignTokens.ContractedWidth, DesignTokens.PillHeight);
    }

    /// <summary>退场：整棵视觉树淡出（宿主随后隐藏窗口）。</summary>
    public async Task PlayDismissAsync(CancellationToken ct)
    {
        try { await AnimationPrimitives.Fade(1d, 0d, DesignTokens.Timing.DismissMs).RunAsync(Root, ct); }
        catch (OperationCanceledException) { return; }

        ResetToInitial();
    }

    // ---------------- 辅助 ----------------

    /// <summary>同步驱动胶囊与阴影的宽度 / 高度（展开与收缩是同一组轨道）。</summary>
    private Task AnimatePill(double width, double height, CancellationToken ct) => Task.WhenAll(
        AnimationPrimitives.WidthTransition(Pill.Width, width).RunAsync(Pill, ct),
        AnimationPrimitives.WidthTransition(PillShadow.Width, width).RunAsync(PillShadow, ct),
        AnimationPrimitives.Transition(Layoutable.HeightProperty, Pill.Height, height, DesignTokens.Timing.WidthMs).RunAsync(Pill, ct),
        AnimationPrimitives.Transition(Layoutable.HeightProperty, PillShadow.Height, height, DesignTokens.Timing.WidthMs).RunAsync(PillShadow, ct));

    /// <summary>动画收尾：把终值写回基值（动画不回写的话，下一次过渡会从错误起点出发）。</summary>
    private void ApplyFinalFrame(double width, double height)
    {
        Pill.Width = width;
        PillShadow.Width = width;
        Pill.Height = height;
        PillShadow.Height = height;
        Pill.Opacity = 1d;
        PillShadow.Opacity = 1d;
    }

    /// <summary>回到初始（隐藏）态：等待尺寸、全透明。</summary>
    private void ResetToInitial()
    {
        Root.Opacity = 1d;
        Root.RenderTransform = new TranslateTransform(0d, 0d);

        Pill.Width = DesignTokens.PillWidth;
        Pill.Height = DesignTokens.PillHeight;
        Pill.CornerRadius = new CornerRadius(DesignTokens.RadiusRound);
        Pill.Opacity = 0d;

        PillShadow.Width = DesignTokens.PillWidth;
        PillShadow.Height = DesignTokens.PillHeight;
        PillShadow.CornerRadius = new CornerRadius(DesignTokens.RadiusRound);
        PillShadow.Opacity = 0d;

        _expanded = false;
        _contracted = false;
        _revealPending = false;
    }
}
