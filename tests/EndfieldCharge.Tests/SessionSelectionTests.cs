using System;
using EndfieldCharge.Host.Plugins.Music;
using Xunit;

namespace EndfieldCharge.Tests;

/// <summary>
/// 「只显示/控制被允许来源」的会话挑选：白名单为空或来源未知一律拒绝；
/// 未授权即便在播也不选；优先级沿用 当前在播 → 任意在播 → 当前 → 任意。
/// </summary>
public class SessionSelectionTests
{
    private static SessionSelection.Candidate C(string? id, bool playing, bool current) => new(id, playing, current);

    [Fact]
    public void Denies_everything_when_the_whitelist_is_empty()
    {
        var candidates = new[] { C("chrome.exe", true, true), C("Spotify.exe", false, false) };

        Assert.Equal(-1, SessionSelection.Pick(candidates, Array.Empty<string>()));
        Assert.Equal(-1, SessionSelection.Pick(candidates, null));
    }

    [Fact]
    public void Ignores_a_playing_source_that_is_not_allowed()
    {
        // 浏览器在播且是当前会话，但未授权 → 只能落到授权（暂停）的 Spotify，而不是浏览器
        var candidates = new[] { C("chrome.exe", true, true), C("Spotify.exe", false, false) };

        Assert.Equal(1, SessionSelection.Pick(candidates, new[] { "Spotify.exe" }));
    }

    [Fact]
    public void Picks_the_allowed_playing_source()
    {
        var candidates = new[] { C("chrome.exe", false, true), C("Spotify.exe", true, false) };

        Assert.Equal(1, SessionSelection.Pick(candidates, new[] { "Spotify.exe" }));
    }

    [Fact]
    public void Prefers_current_playing_over_another_playing()
    {
        var candidates = new[] { C("Spotify.exe", true, false), C("foobar2000.exe", true, true) };

        Assert.Equal(1, SessionSelection.Pick(candidates, new[] { "Spotify.exe", "foobar2000.exe" }));
    }

    [Fact]
    public void Prefers_playing_over_a_paused_current()
    {
        var candidates = new[] { C("Spotify.exe", false, true), C("foobar2000.exe", true, false) };

        Assert.Equal(1, SessionSelection.Pick(candidates, new[] { "Spotify.exe", "foobar2000.exe" }));
    }

    [Fact]
    public void Falls_back_to_the_allowed_current_when_nothing_plays()
    {
        var candidates = new[] { C("Spotify.exe", false, false), C("foobar2000.exe", false, true) };

        Assert.Equal(1, SessionSelection.Pick(candidates, new[] { "Spotify.exe", "foobar2000.exe" }));
    }

    [Fact]
    public void Skips_sources_with_an_unknown_app_id()
    {
        var candidates = new[] { C(null, true, true), C("   ", true, false) };

        Assert.Equal(-1, SessionSelection.Pick(candidates, new[] { "Spotify.exe" }));
    }

    [Fact]
    public void Matching_is_case_insensitive_and_trimmed()
    {
        var candidates = new[] { C("Spotify.exe", true, true) };

        Assert.Equal(0, SessionSelection.Pick(candidates, new[] { "  spotify.exe  " }));
    }

    [Fact]
    public void Returns_minus_one_for_no_candidates()
    {
        Assert.Equal(-1, SessionSelection.Pick(Array.Empty<SessionSelection.Candidate>(), new[] { "Spotify.exe" }));
    }
}
