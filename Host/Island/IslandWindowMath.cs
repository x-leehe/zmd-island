using System;

namespace EndfieldCharge.Host.Island;

/// <summary>
/// 岛窗口定位与顶缘悬停命中的纯几何计算（无 Avalonia 依赖，可单元测试）：
/// 窗口左上角按屏幕工作区 + HUD 位置设置推算；岛（胶囊）在窗口内水平居中；
/// 隐藏态悬停唤醒的「屏幕顶缘细条」命中判定。
/// <para>
/// 生产代码（<c>HudWindow.PositionTopCenter</c> 与命中区计算）与单元测试共用同一套公式，
/// 避免两边各写一份而漂移——命中区不再依赖 <c>Window.Position</c> 是否已落定。
/// </para>
/// </summary>
public static class IslandWindowMath
{
    /// <summary>窗口相对工作区顶边的竖直偏移（物理像素）：正值再上移，使岛更贴近屏幕顶部。</summary>
    public const int TopOffset = 48;

    /// <summary>
    /// 窗口左边缘（物理像素）：左对齐 = 工作区左 +10；右对齐 = 工作区右 - 窗宽 -10；
    /// 其余（顶部居中）= 水平居中。与 <c>HudPosition</c> 语义一一对应。
    /// </summary>
    public static int WindowLeft(int areaX, int areaWidth, int pixelWindowWidth, bool topLeft, bool topRight)
    {
        if (topLeft)
            return areaX + 10;

        if (topRight)
            return areaX + areaWidth - pixelWindowWidth - 10;

        return areaX + (areaWidth - pixelWindowWidth) / 2;
    }

    /// <summary>窗口上边缘（物理像素）：工作区顶边上移 <see cref="TopOffset"/>。</summary>
    public static int WindowTop(int areaY) => areaY - TopOffset;

    /// <summary>岛（胶囊）左边缘（物理像素）：在窗口内水平居中（窗口宽 - 岛宽）/ 2。</summary>
    public static double IslandLeft(int windowLeft, int pixelWindowWidth, double islandWidthPx)
        => windowLeft + pixelWindowWidth / 2d - islandWidthPx / 2d;

    /// <summary>隐藏态悬停唤醒条高度（物理像素）：3 × 显示器缩放。</summary>
    public static double EdgeStripHeight(double scaling) => 3d * scaling;

    /// <summary>
    /// 光标是否落在屏幕顶缘细条内：横向对齐岛区 [islandLeft, islandRight)，纵向从物理屏幕顶部
    /// 起 <see cref="EdgeStripHeight"/> 高（含上边界、不含下边界）。
    /// <para>
    /// 注意：<paramref name="screenTop"/> 应传物理屏幕顶边（Bounds.Y）而非工作区顶边——
    /// 后者会被顶部任务栏 / 第三方美化工具（如 MyDockFinder）下移。
    /// </para>
    /// </summary>
    public static bool IsOverEdgeStrip(double x, double y, int screenTop, double scaling, double islandLeft, double islandRight)
    {
        double s = scaling > 0 ? scaling : 1d;
        int stripHeight = (int)Math.Round(EdgeStripHeight(s));

        return x >= islandLeft && x < islandRight
            && y >= screenTop && y < screenTop + stripHeight;
    }
}
