using EndfieldCharge.Host.Island;
using Xunit;

namespace EndfieldCharge.Tests;

/// <summary>
/// 岛窗口定位 / 顶缘悬停命中的纯几何测试：窗口左边缘（左 / 中 / 右）、岛在 1200 DIP 窗口内居中、
/// 顶缘细条的命中与边界（横向出界、纵向出界、恰在下边界）。提交前用
/// dotnet test tests/EndfieldCharge.Tests 跑一遍。
/// </summary>
public class IslandWindowMathTests
{
    // ---------------- WindowLeft：三种 HUD 位置（含副屏原点偏移） ----------------

    [Theory]
    [InlineData(0, 1920, 1200, false, false, 360)]       // TopCenter：0 + (1920-1200)/2
    [InlineData(0, 1920, 1200, true, false, 10)]         // TopLeft：0 + 10
    [InlineData(0, 1920, 1200, false, true, 710)]        // TopRight：0 + 1920 - 1200 - 10
    [InlineData(1920, 2560, 1200, false, false, 2600)]   // 副屏 TopCenter：1920 + (2560-1200)/2
    [InlineData(1920, 2560, 1200, false, true, 3270)]    // 副屏 TopRight：1920 + 2560 - 1200 - 10
    public void WindowLeft_follows_hud_position(
        int areaX, int areaWidth, int pixelWindowWidth, bool topLeft, bool topRight, int expected)
    {
        Assert.Equal(expected, IslandWindowMath.WindowLeft(areaX, areaWidth, pixelWindowWidth, topLeft, topRight));
    }

    // ---------------- WindowTop：工作区顶边上移固定偏移 ----------------

    [Theory]
    [InlineData(0, -48)]
    [InlineData(1080, 1032)]
    [InlineData(-1080, -1128)]
    public void WindowTop_offsets_working_area_up(int areaY, int expected)
    {
        Assert.Equal(expected, IslandWindowMath.WindowTop(areaY));
    }

    // ---------------- IslandLeft：岛在 1200 DIP 窗口内水平居中 ----------------

    [Theory]
    [InlineData(360, 1200, 560, 680)]    // TopCenter 窗口（x=360）中的完整胶囊
    [InlineData(10, 1200, 560, 330)]     // TopLeft 窗口
    [InlineData(710, 1200, 560, 1030)]   // TopRight 窗口
    [InlineData(360, 1200, 200, 860)]    // 收缩态（宽 200）同样居中
    public void IslandLeft_centers_island_in_1200dip_window(
        int windowLeft, int pixelWindowWidth, double islandWidthPx, double expected)
    {
        Assert.Equal(expected, IslandWindowMath.IslandLeft(windowLeft, pixelWindowWidth, islandWidthPx), precision: 6);
    }

    // ---------------- EdgeStripHeight：3 × 显示器缩放 ----------------

    [Theory]
    [InlineData(1.0, 3.0)]
    [InlineData(1.25, 3.75)]
    [InlineData(1.5, 4.5)]
    public void EdgeStripHeight_scales_with_monitor(double scaling, double expected)
    {
        Assert.Equal(expected, IslandWindowMath.EdgeStripHeight(scaling), precision: 6);
    }

    // ---------------- 顶缘细条：命中 ----------------

    [Fact]
    public void Edge_strip_hits_inside_band()
    {
        // 岛区 [680, 1240)，物理屏幕顶部 0，1.0 缩放 → 条带 [0, 3)
        Assert.True(IslandWindowMath.IsOverEdgeStrip(680, 0, 0, 1.0, 680, 1240)); // 左边界含
        Assert.True(IslandWindowMath.IsOverEdgeStrip(700, 0, 0, 1.0, 680, 1240)); // 上边界含
        Assert.True(IslandWindowMath.IsOverEdgeStrip(700, 2, 0, 1.0, 680, 1240)); // 条带内
        Assert.True(IslandWindowMath.IsOverEdgeStrip(1239, 2, 0, 1.0, 680, 1240)); // 右边界前一像素
    }

    // ---------------- 顶缘细条：出界 / 边界（不含下边界） ----------------

    [Theory]
    [InlineData(679, 1)]    // 横向：左外
    [InlineData(1240, 1)]   // 横向：右边界（不含）
    [InlineData(700, -1)]   // 纵向：上方外
    [InlineData(700, 3)]    // 纵向：恰在下边界（不含）
    public void Edge_strip_misses_out_of_range(double x, double y)
    {
        Assert.False(IslandWindowMath.IsOverEdgeStrip(x, y, 0, 1.0, 680, 1240));
    }

    // ---------------- 顶缘细条：条带高度随缩放（含 Math.Round 语义） ----------------

    [Theory]
    [InlineData(1.0, 2, true)]    // round(3.0) = 3
    [InlineData(1.0, 3, false)]
    [InlineData(1.25, 3, true)]   // round(3.75) = 4
    [InlineData(1.25, 4, false)]
    [InlineData(1.5, 3, true)]    // round(4.5) = 4（ToEven，沿用原实现）
    [InlineData(1.5, 4, false)]
    public void Edge_strip_band_height_follows_scaling(double scaling, int y, bool expected)
    {
        Assert.Equal(expected, IslandWindowMath.IsOverEdgeStrip(700, y, 0, scaling, 680, 1240));
    }

    // ---------------- 顶缘细条：锚定物理屏幕顶部（多显示器可为负） ----------------

    [Fact]
    public void Edge_strip_anchors_to_physical_screen_top()
    {
        // 副屏位于上方：屏幕顶 -1080，缩放 1.0 → 条带 [-1080, -1077)
        Assert.True(IslandWindowMath.IsOverEdgeStrip(700, -1080, -1080, 1.0, 680, 1240));
        Assert.True(IslandWindowMath.IsOverEdgeStrip(700, -1078, -1080, 1.0, 680, 1240));
        Assert.False(IslandWindowMath.IsOverEdgeStrip(700, -1077, -1080, 1.0, 680, 1240));
    }

    // ---------------- 顶缘细条：非正缩放按 1 处理，不崩溃 ----------------

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Edge_strip_treats_non_positive_scaling_as_one(double scaling)
    {
        Assert.True(IslandWindowMath.IsOverEdgeStrip(700, 2, 0, scaling, 680, 1240));
        Assert.False(IslandWindowMath.IsOverEdgeStrip(700, 3, 0, scaling, 680, 1240));
    }
}
