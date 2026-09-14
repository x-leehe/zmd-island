using System;
using System.Text;
using EndfieldCharge.Host.Plugins.Music;
using Xunit;

namespace EndfieldCharge.Tests;

/// <summary>LRC 解析与"当前行"查询（纯逻辑，无 IO / 网络 / UI 依赖）。</summary>
public class LrcParserTests
{
    [Fact]
    public void Parse_reads_basic_timed_lines()
    {
        var timeline = LrcParser.Parse("[00:01.00]第一句\n[00:05.50]第二句");

        Assert.Equal(2, timeline.Lines.Count);
        Assert.Equal(TimeSpan.FromSeconds(1), timeline.Lines[0].Time);
        Assert.Equal("第一句", timeline.Lines[0].Text);
        Assert.Equal(TimeSpan.FromMilliseconds(5500), timeline.Lines[1].Time);
    }

    [Theory]
    [InlineData("[00:01]x", 1000)]       // 无小数位
    [InlineData("[00:01.5]x", 1500)]     // 1 位 = 十分之一秒
    [InlineData("[00:01.25]x", 1250)]    // 2 位 = 百分之一秒
    [InlineData("[00:01.125]x", 1125)]   // 3 位 = 毫秒
    public void Parse_scales_fraction_by_digit_count(string lrc, int expectedMs)
    {
        var line = Assert.Single(LrcParser.Parse(lrc).Lines);
        Assert.Equal(TimeSpan.FromMilliseconds(expectedMs), line.Time);
    }

    [Fact]
    public void Parse_supports_multiple_timestamps_on_one_line()
    {
        var timeline = LrcParser.Parse("[00:10.00][00:20.00]合唱");

        Assert.Equal(2, timeline.Lines.Count);
        Assert.Equal("合唱", timeline.Lines[0].Text);
        Assert.Equal(TimeSpan.FromSeconds(20), timeline.Lines[1].Time);
    }

    [Fact]
    public void Parse_applies_offset_tag()
    {
        var line = Assert.Single(LrcParser.Parse("[offset:-500]\n[00:02.00]句").Lines);

        Assert.Equal(TimeSpan.FromMilliseconds(1500), line.Time);
    }

    [Fact]
    public void Parse_ignores_metadata_and_unparsable_lines()
    {
        var timeline = LrcParser.Parse("[ti:标题]\n[ar:歌手]\n[al:专辑]\n[by:某人]\n随便一行\n[00:03.00]真的歌词");

        Assert.Equal("真的歌词", Assert.Single(timeline.Lines).Text);
    }

    [Fact]
    public void Parse_sorts_out_of_order_lines()
    {
        var timeline = LrcParser.Parse("[00:09.00]后\n[00:01.00]前");

        Assert.Equal("前", timeline.Lines[0].Text);
        Assert.Equal("后", timeline.Lines[1].Text);
    }

    [Fact]
    public void Parse_drops_lines_without_text()
    {
        var timeline = LrcParser.Parse("[00:01.00]\n[00:02.00]有字");

        Assert.Equal("有字", Assert.Single(timeline.Lines).Text);
    }

    [Fact]
    public void Parse_strips_enhanced_word_timestamps()
    {
        // 增强型 LRC：行首是整句时间戳，句内是逐字时间戳（网易云等会输出这种）
        var timeline = LrcParser.Parse("[00:12.34]<00:12.34>词<00:12.80>语");

        var line = Assert.Single(timeline.Lines);
        Assert.Equal("词语", line.Text);
        Assert.Equal(TimeSpan.FromMilliseconds(12340), line.Time);
    }

    [Fact]
    public void Parse_keeps_angle_brackets_that_are_not_timestamps()
    {
        var timeline = LrcParser.Parse("[00:01.00]a < b <3 <not time>");

        Assert.Equal("a < b <3 <not time>", Assert.Single(timeline.Lines).Text);
    }

    [Fact]
    public void Parse_gives_empty_timeline_for_garbage()
    {
        Assert.True(LrcParser.Parse(null).IsEmpty);
        Assert.True(LrcParser.Parse(string.Empty).IsEmpty);
        Assert.True(LrcParser.Parse("   \n\n  ").IsEmpty);
        Assert.True(LrcParser.Parse("[ti:只有元信息]").IsEmpty);
    }

    [Fact]
    public void CurrentAt_holds_the_previous_line_between_lines()
    {
        var timeline = LrcParser.Parse("[00:01.00]第一句\n[00:05.00]第二句");

        Assert.Equal("第一句", timeline.CurrentAt(TimeSpan.FromSeconds(3)));
        Assert.Equal("第二句", timeline.CurrentAt(TimeSpan.FromSeconds(5)));
        Assert.Equal("第二句", timeline.CurrentAt(TimeSpan.FromMinutes(9)));
    }

    [Fact]
    public void CurrentAt_is_empty_before_the_first_line()
    {
        var timeline = LrcParser.Parse("[00:10.00]第一句");

        Assert.Equal(string.Empty, timeline.CurrentAt(TimeSpan.FromSeconds(2)));
        Assert.Equal(string.Empty, LrcTimeline.Empty.CurrentAt(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void CurrentAt_stays_correct_on_a_long_timeline()
    {
        // 1000 行、倒序写入：构造器排序后二分查找仍应取准
        var sb = new StringBuilder();
        for (int i = 1000; i >= 1; i--)
            sb.Append($"[{i / 60:00}:{i % 60:00}.00]第{i}句\n");

        var timeline = LrcParser.Parse(sb.ToString());

        Assert.Equal(1000, timeline.Lines.Count);
        Assert.Equal("第7句", timeline.CurrentAt(TimeSpan.FromSeconds(7)));
        Assert.Equal("第999句", timeline.CurrentAt(TimeSpan.FromSeconds(999)));
    }
}
