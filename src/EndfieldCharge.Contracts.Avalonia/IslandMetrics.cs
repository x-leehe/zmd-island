namespace EndfieldCharge.Host.Island;

/// <summary>
/// 岛尺寸（DIP）：宿主用它算点击穿透命中区、悬停唤醒区与右键菜单锚点。
/// <para>
/// <c>Width</c>/<c>Height</c> 为<strong>未缩放</strong>设计尺寸；
/// <c>Top</c> 为岛顶在窗口内的未缩放布局偏移；
/// <c>Scale</c> 为皮肤自身的内部渲染缩放（电池跟随全局缩放 0.8；音乐按设计 1:1 = 1.0）。
/// 宿主最终尺寸 = Width/Height × Scale × 显示器 DPI。
/// </para>
/// </summary>
public readonly record struct IslandMetrics(double Width, double Height, double Top, double Scale);
