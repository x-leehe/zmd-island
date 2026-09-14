using System;
using EndfieldCharge.Host.Plugins.Music;
using Xunit;

namespace EndfieldCharge.Tests;

/// <summary>
/// 显示曲线：**S 形映射**（最底到最顶不是线性的）+ 邻域平滑（消掉相邻频段的跳变）。
/// 两条都是用户直接提出的观感诉求，所以各自锁一个测试。
/// </summary>
public class SpectrumCurveTests
{
    [Fact]
    public void S_curve_hits_the_three_anchor_points()
    {
        Assert.Equal(0d, SpectrumCurve.S(0d), 6);
        Assert.Equal(0.5d, SpectrumCurve.S(0.5d), 6);
        Assert.Equal(1d, SpectrumCurve.S(1d), 6);
    }

    [Fact]
    public void S_curve_flattens_both_ends_and_steepens_the_middle()
    {
        Assert.True(SpectrumCurve.S(0.2d) < 0.2d, "下段应被压住");
        Assert.True(SpectrumCurve.S(0.8d) > 0.8d, "上段应被顶起");

        double middle = SpectrumCurve.S(0.55d) - SpectrumCurve.S(0.50d);
        double bottom = SpectrumCurve.S(0.10d) - SpectrumCurve.S(0.05d);
        double top = SpectrumCurve.S(0.95d) - SpectrumCurve.S(0.90d);

        Assert.True(middle > bottom, $"中段应比下段陡：middle={middle}, bottom={bottom}");
        Assert.True(middle > top, $"中段应比上段陡：middle={middle}, top={top}");
    }

    [Theory]
    [InlineData(-1d, 0d)]
    [InlineData(2d, 1d)]
    public void S_curve_clamps_input_outside_range(double input, double expected) =>
        Assert.Equal(expected, SpectrumCurve.S(input), 6);

    [Fact]
    public void Neighbour_smoothing_shrinks_adjacent_jumps()
    {
        var values = new[] { 0.1d, 0.9d, 0.1d, 0.9d, 0.1d };
        double before = MaxJump(values);

        SpectrumCurve.SmoothNeighbours(values, 2);

        Assert.True(MaxJump(values) < before * 0.6d, $"相邻跳变应明显变小：{before} → {MaxJump(values)}");
    }

    [Fact]
    public void Neighbour_smoothing_keeps_a_flat_line_flat()
    {
        var values = new[] { 0.4d, 0.4d, 0.4d };

        SpectrumCurve.SmoothNeighbours(values, 3);

        Assert.All(values, value => Assert.Equal(0.4d, value, 6));
    }

    private static double MaxJump(double[] values)
    {
        double max = 0d;
        for (int i = 1; i < values.Length; i++)
            max = Math.Max(max, Math.Abs(values[i] - values[i - 1]));

        return max;
    }
}
