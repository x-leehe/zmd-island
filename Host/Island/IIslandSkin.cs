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
}
