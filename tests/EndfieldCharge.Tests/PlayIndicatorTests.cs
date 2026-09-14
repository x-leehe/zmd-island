using EndfieldCharge.Host.Plugins.Music;
using Xunit;

namespace EndfieldCharge.Tests;

/// <summary>
/// 「在播放」指示器（等待态 / 收缩态那三根柱）的假动画：
/// 它标示的是"音乐在放"，不接真实采样 —— 所以只要求：暂停全零、播放时在跳且在量程内。
/// </summary>
public class PlayIndicatorTests
{
    [Fact]
    public void Paused_bars_are_all_zero()
    {
        var levels = PlayIndicator.Build(playing: false, phase: 1.23d);

        Assert.All(levels, level => Assert.Equal(0f, level));
    }

    [Fact]
    public void Playing_bars_stay_in_range_and_move_with_phase()
    {
        var first = PlayIndicator.Build(playing: true, phase: 0d);
        var later = PlayIndicator.Build(playing: true, phase: 1d);

        Assert.All(first, level => Assert.InRange(level, 0.08f, 1f));
        Assert.NotEqual(first[0], later[0]);              // 相位推进 → 柱高变化（看着"在跳"）
    }

    [Fact]
    public void Bars_are_phase_shifted()
    {
        var levels = PlayIndicator.Build(playing: true, phase: 0d);

        Assert.NotEqual(levels[0], levels[1]);
        Assert.NotEqual(levels[1], levels[2]);
    }

    [Fact]
    public void Bar_count_is_respected()
    {
        Assert.Equal(5, PlayIndicator.Build(playing: true, phase: 0.5d, bars: 5).Length);
    }
}
