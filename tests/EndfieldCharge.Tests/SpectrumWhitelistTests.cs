using System;
using System.Collections.Generic;
using EndfieldCharge.Host.Plugins.Music;
using Xunit;

namespace EndfieldCharge.Tests;

/// <summary>
/// 音频来源白名单：默认空 ⇒ 一律不允许；精确匹配、忽略大小写；来源未知一律拒绝。
/// </summary>
public class SpectrumWhitelistTests
{
    private static readonly string[] TwoSources =
    {
        @"Spotify.exe",
        @"Tencent\QQMusic.exe",
    };

    [Fact]
    public void Empty_whitelist_allows_nothing()
    {
        Assert.False(SpectrumWhitelist.IsAllowed(Array.Empty<string>(), "Spotify.exe"));
        Assert.False(SpectrumWhitelist.IsAllowed(null, "Spotify.exe"));
        Assert.False(SpectrumWhitelist.IsAllowed(new List<string>(), "Spotify.exe"));
    }

    [Fact]
    public void Unknown_source_is_never_allowed()
    {
        Assert.False(SpectrumWhitelist.IsAllowed(TwoSources, null));
        Assert.False(SpectrumWhitelist.IsAllowed(TwoSources, string.Empty));
        Assert.False(SpectrumWhitelist.IsAllowed(TwoSources, "   "));
    }

    [Fact]
    public void Matching_is_exact_and_case_insensitive()
    {
        Assert.True(SpectrumWhitelist.IsAllowed(TwoSources, "Spotify.exe"));
        Assert.True(SpectrumWhitelist.IsAllowed(TwoSources, "spotify.exe"));
        Assert.True(SpectrumWhitelist.IsAllowed(TwoSources, @"  Tencent\QQMusic.exe  "));

        // 不做模糊匹配：多一个字、少一个字都不算
        Assert.False(SpectrumWhitelist.IsAllowed(TwoSources, "Spotify"));
        Assert.False(SpectrumWhitelist.IsAllowed(TwoSources, "Spotify.exe.old"));
        Assert.False(SpectrumWhitelist.IsAllowed(TwoSources, @"Tencent\QQMusic"));
    }

    [Fact]
    public void Blank_entries_in_the_whitelist_are_ignored()
    {
        var sources = new[] { "", "   ", "Spotify.exe" };

        Assert.True(SpectrumWhitelist.IsAllowed(sources, "Spotify.exe"));
        Assert.False(SpectrumWhitelist.IsAllowed(new[] { "", "   " }, "Spotify.exe"));
    }
}
