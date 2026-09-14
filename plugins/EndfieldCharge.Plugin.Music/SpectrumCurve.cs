using System;

namespace EndfieldCharge.Host.Plugins.Music;

/// <summary>
/// 频谱的**显示曲线**：纯函数，不碰音频也不碰 UI。
/// <list type="bullet">
///   <item><see cref="S"/>：最底到最顶的映射**不是线性的**，而是一条 S 形曲线（两端平、中段陡）；</item>
///   <item><see cref="SmoothNeighbours"/>：抹平相邻频段 —— "突兀"正是来自相邻段的跳变。</item>
/// </list>
/// </summary>
public static class SpectrumCurve
{
    /// <summary>
    /// S 形映射（smoothstep：3v² − 2v³）。满足 f(0)=0、f(1)=1、f(0.5)=0.5，
    /// 且**两端导数为 0** —— 安静段压得住、响段顶到上沿也不会乱跳，而音乐主体所在的中间段被拉开。
    /// 这就是"生物学 S 曲线"的形状；线性映射则把两头都挤在中间那截。
    /// </summary>
    public static double S(double value)
    {
        value = Math.Clamp(value, 0d, 1d);
        return (3d * value * value) - (2d * value * value * value);
    }

    /// <summary>
    /// [1,2,1] 邻域平滑（原地、可多轮；两端取镜像；权重和为 1 所以不会整体压低调子）。
    /// 频段值是**段内平均**，相邻段之间仍有跳变 —— 这一步消掉的就是它。
    /// 轮数越多越平：1 轮留着细节，3 轮以上会把局部峰谷也抹掉（成一条不动的带子）。
    /// </summary>
    public static void SmoothNeighbours(double[] values, int passes = 1)
    {
        if (values.Length < 3)
            return;

        for (int pass = 0; pass < passes; pass++)
        {
            var copy = (double[])values.Clone();
            for (int i = 0; i < values.Length; i++)
            {
                double left = copy[i > 0 ? i - 1 : 0];
                double right = copy[i + 1 < copy.Length ? i + 1 : copy.Length - 1];
                values[i] = (left + (2d * copy[i]) + right) / 4d;
            }
        }
    }
}
