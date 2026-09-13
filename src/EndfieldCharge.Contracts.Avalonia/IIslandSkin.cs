using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using EndfieldCharge.Contracts;

namespace EndfieldCharge.Host.Island;

/// <summary>
/// 灵动岛皮肤契约：一个皮肤 = 一个可见的岛视觉树 + 一套播放方法。
/// 宿主（HudWindow）只负责窗口 chrome（置顶 / 点击穿透 / 定位 / 菜单），
/// 视觉与动画全部由皮肤实现。
/// </summary>
public interface IIslandSkin
{
    string Id { get; }

    /// <summary>岛的视觉树根（宿主放入 ContentControl 显示）。</summary>
    Control View { get; }

    /// <summary>将一帧内容描述应用到视觉树（数值 / 环 / 文案 / 色调）。</summary>
    void BindContent(IslandContentDescriptor content);

    /// <summary>响应动画（插拔电）：PlayKind.Full = 完整三态；Simple = 简化胶囊。</summary>
    Task PlayResponseAsync(CancellationToken ct);

    /// <summary>等待态：状态 C 静态画面 + 快速出现动画，结束后保持可见。</summary>
    Task PlayWaitingAsync(CancellationToken ct);

    /// <summary>收缩态：胶囊收窄为仅环 + 百分比。</summary>
    Task PlayContractAsync(CancellationToken ct);

    /// <summary>退场动画（淡出整棵视觉树，由宿主随后 Hide）。</summary>
    Task PlayDismissAsync(CancellationToken ct);

    /// <summary>应用全局缩放（设置项 GlobalScale）。</summary>
    void ApplyScale(double globalScale);

    /// <summary>布置揭示起点（宿主在 Show 窗口前调用，避免首帧闪现整只胶囊）。</summary>
    void PrepareRevealStart();

    /// <summary>当前岛尺寸（点击穿透命中区 / 菜单锚点；随收缩 / 展开变化）。</summary>
    IslandMetrics CurrentMetrics { get; }

    /// <summary>完整岛区尺寸（悬停唤醒区；不随收缩收窄）。</summary>
    IslandMetrics HoverMetrics { get; }

    // ---------------- 可选特性（默认接口实现：皮肤覆写即可，无需改动既有实现） ----------------

    /// <summary>
    /// 是否参与「鼠标滚轮切换岛」轮转。
    /// <c>false</c> = 被排除在轮转之外（例如只作为被动内容源、不希望被切到）。
    /// </summary>
    bool ParticipatesInWheelSwitch => true;

    /// <summary>
    /// 是否使用宿主提供的电池内容（<see cref="BindContent"/>）。
    /// <c>false</c> = 皮肤自给数据（如音乐走自身 SMTC），宿主不再抓取电池快照。
    /// </summary>
    bool UsesHostContent => true;

    /// <summary>承载本皮肤的窗口高度（DIP）：不同皮肤展开态高度不同（电池 160 / 音乐 220）。</summary>
    double WindowHeight => 160d;

    /// <summary>
    /// 展开态（宿主 <see cref="IslandVisualState.Response"/>）无交互多久回落到等待态（秒）。
    /// 每个状态都有自己的生命周期（超时）：宿主对小于 3 秒的声明会按下限执行，
    /// 因此不存在"不回落"的皮肤。
    /// </summary>
    double ExpandedTimeoutSeconds => 6d;
}
