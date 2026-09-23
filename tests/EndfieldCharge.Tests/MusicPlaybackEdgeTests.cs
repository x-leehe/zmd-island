using EndfieldCharge.Host.Plugins.Music;
using Xunit;

namespace EndfieldCharge.Tests;

public class MusicPlaybackEdgeTests
{
    [Fact]
    public void TemporaryEmptyFrameDoesNotMakeContinuingPlaybackLookLikeNewStart()
    {
        var edge = new MusicPlaybackEdge();
        Assert.False(edge.Observe("musicfox.exe", true));
        Assert.False(edge.Observe(null, false));
        Assert.False(edge.Observe("musicfox.exe", true));
    }

    [Fact]
    public void RealPauseThenResumeStillCountsAsPlaybackStart()
    {
        var edge = new MusicPlaybackEdge();
        Assert.False(edge.Observe(null, false));
        Assert.False(edge.Observe("musicfox.exe", true));
        Assert.False(edge.Observe("musicfox.exe", false));
        Assert.True(edge.Observe("musicfox.exe", true));
    }
}
