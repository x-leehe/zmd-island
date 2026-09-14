using System;
using EndfieldCharge.Host.Plugins.Music;
using Xunit;

namespace EndfieldCharge.Tests;

/// <summary>
/// 行内进度：长句"跟唱"滚动的依据 —— 行首为 0、下一行的时刻为 1。
/// 这就是"歌手唱到哪里，可见部分就跟到哪里"里的数学部分（纯逻辑，不碰 UI）。
/// </summary>
public class LrcProgressTests
{
    private static readonly LrcTimeline Timeline = LrcParser.Parse(
        "[00:00.00]第一句\n[00:10.00]第二句\n[00:20.00]第三句");

    [Fact]
    public void Line_start_is_zero() =>
        Assert.Equal(0d, Timeline.ProgressAt(TimeSpan.FromSeconds(10)), 3);

    [Fact]
    public void Midway_through_a_line_is_half() =>
        Assert.Equal(0.5d, Timeline.ProgressAt(TimeSpan.FromSeconds(15)), 3);

    [Fact]
    public void Just_before_the_next_line_is_almost_one() =>
        Assert.InRange(Timeline.ProgressAt(TimeSpan.FromSeconds(19.9)), 0.98d, 1d);

    [Fact]
    public void Before_the_first_line_is_zero() =>
        Assert.Equal(0d, Timeline.ProgressAt(TimeSpan.Zero), 3);

    [Fact]
    public void The_last_line_uses_an_estimated_tail()
    {
        // 最后一行没有"下一行"可参考：按 5s 估算跨度（20s → 25s）→ 22.5s 处正好一半
        Assert.Equal(0.5d, Timeline.ProgressAt(TimeSpan.FromSeconds(22.5)), 3);
        Assert.Equal(1d, Timeline.ProgressAt(TimeSpan.FromSeconds(25)), 3);
        Assert.Equal(1d, Timeline.ProgressAt(TimeSpan.FromSeconds(60)), 3);   // 更晚则钳到 1
    }

    [Fact]
    public void Empty_timeline_is_zero() =>
        Assert.Equal(0d, LrcTimeline.Empty.ProgressAt(TimeSpan.FromSeconds(3)), 3);

    [Fact]
    public void Progress_and_text_agree_on_the_same_line()
    {
        // 同一位置：CurrentAt 给出这一行，ProgressAt 给出它行内的进度
        var position = TimeSpan.FromSeconds(15);

        Assert.Equal("第二句", Timeline.CurrentAt(position));
        Assert.Equal(0.5d, Timeline.ProgressAt(position), 3);
    }
}
