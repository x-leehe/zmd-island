using System;
using EndfieldCharge.Services;
using EndfieldCharge.Settings;
using Xunit;

namespace EndfieldCharge.Tests;

/// <summary>免打扰：档位换算、到期判定、暂停、取消（纯逻辑，不碰 UI，也没有定时器）。</summary>
public class DndTests
{
    private static readonly DateTime Now = new(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Default_settings_are_not_in_dnd()
    {
        Assert.False(Dnd.IsActive(new AppSettings(), Now));
        Assert.Null(Dnd.Remaining(new AppSettings(), Now));
    }

    [Theory]
    [InlineData(DndOption.FiveMinutes, 5)]
    [InlineData(DndOption.FifteenMinutes, 15)]
    [InlineData(DndOption.OneHour, 60)]
    [InlineData(DndOption.ThreeHours, 180)]
    public void Apply_sets_deadline_by_option(DndOption option, int expectedMinutes)
    {
        var applied = Dnd.Apply(new AppSettings(), option, Now);

        Assert.True(Dnd.IsActive(applied, Now));
        Assert.Equal(Now.AddMinutes(expectedMinutes), applied.DndUntilUtc);
        Assert.False(applied.DndPaused);
    }

    [Fact]
    public void Dnd_expires_by_itself_without_any_timer()
    {
        var applied = Dnd.Apply(new AppSettings(), DndOption.FiveMinutes, Now);

        Assert.True(Dnd.IsActive(applied, Now.AddMinutes(4)));
        Assert.False(Dnd.IsActive(applied, Now.AddMinutes(5)));      // 到点即失效（比较时间得出）
        Assert.False(Dnd.IsActive(applied, Now.AddHours(2)));
    }

    [Fact]
    public void Pause_lasts_until_manually_turned_off()
    {
        var paused = Dnd.Apply(new AppSettings(), DndOption.Pause, Now);

        Assert.True(Dnd.IsActive(paused, Now.AddDays(3)));
        Assert.Null(Dnd.Remaining(paused, Now.AddDays(3)));          // 暂停没有剩余量
        Assert.False(Dnd.IsActive(Dnd.Apply(paused, DndOption.Off, Now), Now.AddDays(3)));
    }

    [Fact]
    public void Off_clears_both_the_deadline_and_the_pause()
    {
        var applied = Dnd.Apply(new AppSettings(), DndOption.ThreeHours, Now);

        var off = Dnd.Apply(applied, DndOption.Off, Now);

        Assert.Null(off.DndUntilUtc);
        Assert.False(off.DndPaused);
        Assert.False(Dnd.IsActive(off, Now));
    }

    [Fact]
    public void Applying_an_option_cancels_a_previous_pause()
    {
        var paused = Dnd.Apply(new AppSettings(), DndOption.Pause, Now);

        var timed = Dnd.Apply(paused, DndOption.FifteenMinutes, Now);

        Assert.False(timed.DndPaused);
        Assert.Equal(Now.AddMinutes(15), timed.DndUntilUtc);
    }

    [Fact]
    public void Remaining_counts_down_and_never_goes_negative()
    {
        var applied = Dnd.Apply(new AppSettings(), DndOption.OneHour, Now);

        Assert.Equal(TimeSpan.FromMinutes(30), Dnd.Remaining(applied, Now.AddMinutes(30)));
        Assert.Null(Dnd.Remaining(applied, Now.AddHours(2)));
    }

    [Fact]
    public void Menu_order_covers_the_five_levels_the_menu_shows()
    {
        Assert.Equal(
            new[]
            {
                DndOption.FiveMinutes,
                DndOption.FifteenMinutes,
                DndOption.OneHour,
                DndOption.ThreeHours,
                DndOption.Pause,
            },
            Dnd.MenuOptions);

        foreach (var option in Dnd.MenuOptions)
            Assert.False(string.IsNullOrWhiteSpace(Dnd.Label(option)));
    }

    [Fact]
    public void Describe_mentions_the_state_only_when_active()
    {
        var idle = Dnd.Describe(new AppSettings(), Now);
        var timed = Dnd.Describe(Dnd.Apply(new AppSettings(), DndOption.FiveMinutes, Now), Now.AddMinutes(1));
        var paused = Dnd.Describe(Dnd.Apply(new AppSettings(), DndOption.Pause, Now), Now);

        Assert.DoesNotContain("·", idle);
        Assert.Contains("·", timed);
        Assert.Contains("4", timed);          // 剩余 4 分钟
        Assert.Contains("·", paused);
    }
}
