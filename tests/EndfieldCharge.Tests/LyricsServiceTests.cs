using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EndfieldCharge.Host.Plugins.Music;
using Xunit;

namespace EndfieldCharge.Tests;

/// <summary>
/// 歌词调度：来源切换、换曲取消、同曲去重、旧设置迁移。
/// 用可等待 / 可挂起的假 provider，不联网。
/// </summary>
public class LyricsServiceTests
{
    private static readonly LyricsQuery TrackA = new("A", "歌手", "专辑", TimeSpan.FromSeconds(200));
    private static readonly LyricsQuery TrackB = new("B", "歌手", "专辑", TimeSpan.FromSeconds(210));

    private static LrcTimeline Timeline(string text) => LrcParser.Parse("[00:01.00]" + text);

    [Fact]
    public async Task Same_track_notification_does_not_refetch()
    {
        var provider = new FakeProvider(_ => Task.FromResult<LrcTimeline?>(Timeline("第一句")));
        using var service = new LyricsService(_ => provider);
        service.Use("lrclib");
        int raised = 0;
        service.Changed += () => raised++;

        await service.OnTrackChangedAsync(TrackA);
        await service.OnTrackChangedAsync(TrackA);

        Assert.Equal(1, provider.Calls);
        Assert.Equal("第一句", service.CurrentAt(TimeSpan.FromSeconds(2)));
        Assert.Equal(1, raised);
    }

    [Fact]
    public async Task Switching_track_cancels_the_pending_fetch()
    {
        var gate = new TaskCompletionSource<LrcTimeline?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new FakeProvider(query => query.Key == TrackA.Key
            ? gate.Task
            : Task.FromResult<LrcTimeline?>(Timeline("B 的歌词")));

        using var service = new LyricsService(_ => provider);
        service.Use("lrclib");

        var pending = service.OnTrackChangedAsync(TrackA);
        await service.OnTrackChangedAsync(TrackB);          // 换曲 → 取消 A

        Assert.True(provider.WasCancelled);

        gate.SetResult(Timeline("A 的歌词（迟到的响应）"));   // A 迟到：不应写回
        await pending;

        Assert.Equal("B 的歌词", service.CurrentAt(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public async Task Off_source_never_fetches_and_clears_lyrics()
    {
        var provider = new FakeProvider(_ => Task.FromResult<LrcTimeline?>(Timeline("不该被取到")));
        using var service = new LyricsService(source => source == "off" ? null : provider);

        service.Use("lrclib");
        await service.OnTrackChangedAsync(TrackA);
        Assert.True(service.HasLyrics);

        service.Use("off");
        int before = provider.Calls;
        await service.OnTrackChangedAsync(TrackA);

        Assert.Equal(before, provider.Calls);
        Assert.False(service.HasLyrics);
        Assert.Equal(string.Empty, service.CurrentAt(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public async Task Legacy_sources_migrate_to_merge()
    {
        var provider = new FakeProvider(_ => Task.FromResult<LrcTimeline?>(Timeline("迁移后可取")));
        using var service = new LyricsService(source => source == "merge" ? provider : null);

        service.Use("smtc");                                 // 旧设置（根本不是有效来源）
        Assert.Equal("merge", service.Source);

        await service.OnTrackChangedAsync(TrackA);
        Assert.Equal("迁移后可取", service.CurrentAt(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void Legacy_auto_source_migrates_to_merge()
    {
        using var service = new LyricsService(_ => null);

        service.Use("auto");                                 // 旧的「顺序回退」取值 → 现在是并行择优

        Assert.Equal("merge", service.Source);
    }

    [Fact]
    public async Task Fetch_failure_is_swallowed_and_reported_as_empty()
    {
        var provider = new FakeProvider(_ => throw new InvalidOperationException("网络炸了"));
        using var service = new LyricsService(_ => provider);
        service.Use("lrclib");

        await service.OnTrackChangedAsync(TrackA);           // 不应抛出

        Assert.False(service.HasLyrics);
        Assert.Equal(string.Empty, service.CurrentAt(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public async Task Track_change_clears_previous_lyrics_immediately()
    {
        var gate = new TaskCompletionSource<LrcTimeline?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new FakeProvider(q => q.Key == TrackA.Key
            ? Task.FromResult<LrcTimeline?>(Timeline("A 的歌词"))
            : gate.Task);

        using var service = new LyricsService(_ => provider);
        service.Use("lrclib");
        await service.OnTrackChangedAsync(TrackA);

        var pending = service.OnTrackChangedAsync(TrackB);
        Assert.False(service.HasLyrics);                     // B 还没回来 → 已清空，不留上一首的残影

        gate.SetResult(Timeline("B 的歌词"));
        await pending;
        Assert.Equal("B 的歌词", service.CurrentAt(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public async Task Unusable_query_does_not_fetch()
    {
        var provider = new FakeProvider(_ => Task.FromResult<LrcTimeline?>(Timeline("x")));
        using var service = new LyricsService(_ => provider);
        service.Use("lrclib");

        await service.OnTrackChangedAsync(null);
        await service.OnTrackChangedAsync(new LyricsQuery(string.Empty, "歌手", "专辑", TimeSpan.Zero));

        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task A_miss_is_retried_only_a_few_times()
    {
        var provider = new FakeProvider(_ => Task.FromResult<LrcTimeline?>(null));
        using var service = new LyricsService(_ => provider);
        service.Use("merge");

        for (int i = 0; i < 10; i++)
            await service.OnTrackChangedAsync(TrackA);      // SMTC 会反复通知同一首

        Assert.Equal(LyricsService.MaxAttemptsPerTrack, provider.Calls);
    }

    [Fact]
    public async Task A_new_track_resets_the_retry_budget()
    {
        var provider = new FakeProvider(_ => Task.FromResult<LrcTimeline?>(null));
        using var service = new LyricsService(_ => provider);
        service.Use("merge");

        for (int i = 0; i < 10; i++)
            await service.OnTrackChangedAsync(TrackA);
        await service.OnTrackChangedAsync(TrackB);          // 换曲 → 预算重置

        Assert.Equal(LyricsService.MaxAttemptsPerTrack + 1, provider.Calls);
    }

    [Fact]
    public async Task Changing_the_source_resets_the_retry_budget()
    {
        var provider = new FakeProvider(_ => Task.FromResult<LrcTimeline?>(null));
        using var service = new LyricsService(_ => provider);
        service.Use("merge");

        for (int i = 0; i < 10; i++)
            await service.OnTrackChangedAsync(TrackA);

        service.Use("lrclib");                              // 换来源 → 同一首也要重取
        await service.OnTrackChangedAsync(TrackA);

        Assert.Equal(LyricsService.MaxAttemptsPerTrack + 1, provider.Calls);
    }

    // ---------------- 测试替身 ----------------
    private sealed class FakeProvider : ILyricsProvider
    {
        private readonly Func<LyricsQuery, Task<LrcTimeline?>> _handler;

        public FakeProvider(Func<LyricsQuery, Task<LrcTimeline?>> handler) => _handler = handler;

        public int Calls { get; private set; }

        public CancellationToken LastToken { get; private set; }

        /// <summary>任一请求被取消过（换曲打断）。读 token 本身可能已被释放，故用回调记录。</summary>
        public bool WasCancelled { get; private set; }

        public Task<LrcTimeline?> FetchAsync(LyricsQuery query, CancellationToken ct)
        {
            Calls++;
            LastToken = ct;
            ct.Register(() => WasCancelled = true);
            return _handler(query);
        }
    }
}
