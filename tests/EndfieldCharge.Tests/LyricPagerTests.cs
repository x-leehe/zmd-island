using EndfieldCharge.Host.Plugins.Music;
using Xunit;

namespace EndfieldCharge.Tests;

/// <summary>
/// 分页跟唱的两条不变量：
/// ① 唱到逼近当前屏右缘才翻页（其余时间**不动**）；② 只向前，播放位置的抖动不许收回去。
/// </summary>
public class LyricPagerTests
{
    private const double Visible = 0.5d;    // 一屏显示整句的一半
    private const double Margin = 0.12d;    // 右缘留 12% 余量

    [Fact]
    public void Does_nothing_while_the_visible_part_is_far_from_being_sung()
    {
        // 右缘在 0.5，余量 0.12 ⇒ 唱到 0.38 之前都不该动
        Assert.False(LyricPager.TryAdvance(0d, Visible, 0.20d, Margin, out double next));
        Assert.Equal(0d, next);
    }

    [Fact]
    public void Advances_when_the_visible_part_is_almost_sung_out()
    {
        Assert.True(LyricPager.TryAdvance(0d, Visible, 0.38d, Margin, out double next));
        Assert.Equal(Visible * LyricPager.AdvanceRatio, next, 6);
    }

    [Fact]
    public void Never_moves_backwards()
    {
        // 已经翻到 0.375：此时播放位置回退，也不该把内容收回去
        Assert.False(LyricPager.TryAdvance(0.375d, Visible, 0.30d, Margin, out double next));
        Assert.Equal(0.375d, next, 6);
    }

    [Fact]
    public void Does_not_advance_twice_for_the_same_progress()
    {
        // 翻页后窗口右缘变成 0.875（余量 0.12 ⇒ 0.755 才该再翻）：
        // 同一个 progress 再喂进来不能又动一次（否则 60fps 下会连翻）。
        Assert.True(LyricPager.TryAdvance(0d, Visible, 0.4d, Margin, out double first));
        Assert.False(LyricPager.TryAdvance(first, Visible, 0.4d, Margin, out _));
    }

    [Fact]
    public void Stops_at_the_end_of_the_line()
    {
        // 起点已经到上限（1 − 0.5 = 0.5）⇒ 再怎么唱也不动
        Assert.False(LyricPager.TryAdvance(0.5d, Visible, 1d, Margin, out _));
    }

    [Fact]
    public void A_line_that_fits_never_pages()
    {
        Assert.False(LyricPager.TryAdvance(0d, 1d, 1d, Margin, out _));       // visible = 1 ⇒ 放得下
        Assert.False(LyricPager.TryAdvance(0d, 0d, 1d, Margin, out _));       // 参数不合理
    }
}
