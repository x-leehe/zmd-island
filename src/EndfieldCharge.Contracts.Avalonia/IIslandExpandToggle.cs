using System;

namespace EndfieldCharge.Contracts.Avalonia;

/// <summary>
/// 可选皮肤能力：皮肤自己有「展开 / 收起」按钮时实现它，
/// 宿主据此切换 等待态 ↔ 展开态（宿主不反向引用插件的具体类型）。
/// </summary>
public interface IIslandExpandToggle
{
    event Action? ExpandToggled;
}
