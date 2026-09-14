using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EndfieldCharge.Host.Plugins.Music;
using Xunit;

namespace EndfieldCharge.Tests;

/// <summary>认领规则：曲名必须对得上，艺术家要有交集（未知时才看时长）—— 宁缺勿错。</summary>
public class LyricsMatcherTests
{
    private static LyricsQuery Query(string title = "歌名", string artist = "歌手", int seconds = 200) =>
        new(title, artist, "专辑", TimeSpan.FromSeconds(seconds));

    [Fact]
    public void Title_similarity_tolerates_trailing_punctuation_and_version_suffix()
    {
        // 网易云把句号写进曲名（作者原样），SMTC 没有 —— 同一首，必须够像
        Assert.True(LyricsMatcher.TitleSimilarity("灰すら遺らなくても。", "灰すら遺らなくても") >= LyricsMatcher.TitleSimilarityThreshold);
        Assert.True(LyricsMatcher.TitleSimilarity("歌名 （Sped up）", "歌名") >= LyricsMatcher.TitleSimilarityThreshold);

        // 完全不同的歌不能被放过
        Assert.True(LyricsMatcher.TitleSimilarity("完全别的歌", "歌名") < LyricsMatcher.TitleSimilarityThreshold);
    }

    [Fact]
    public void Matching_candidate_is_trusted()
    {
        var candidate = new LyricsCandidate("1", "歌名", "歌手", "专辑", TimeSpan.FromSeconds(200));

        Assert.True(LyricsMatcher.IsTrustworthy(candidate, Query()));
    }

    [Fact]
    public void Same_title_by_another_artist_is_rejected_even_with_matching_duration()
    {
        // 实测踩过的坑：同名、时长也一样，但不是同一个人
        var candidate = new LyricsCandidate("1", "歌名", "别人", "别的专辑", TimeSpan.FromSeconds(200));

        Assert.False(LyricsMatcher.IsTrustworthy(candidate, Query()));
    }

    [Fact]
    public void Artist_tokens_are_compared_individually()
    {
        var candidate = new LyricsCandidate("1", "歌名", "TIC", "专辑", TimeSpan.FromSeconds(180));

        // 查询是 "TIC,Paperman" → 拆开后 TIC 命中即可信（时长差 20 秒也不管）
        Assert.True(LyricsMatcher.IsTrustworthy(candidate, Query(artist: "TIC,Paperman")));
    }

    [Fact]
    public void Version_suffix_in_the_title_is_ignored()
    {
        var candidate = new LyricsCandidate("1", "歌名 （Sped up）", "歌手", "专辑", TimeSpan.FromSeconds(180));

        Assert.True(LyricsMatcher.IsTrustworthy(candidate, Query()));
    }

    [Fact]
    public void Unknown_artist_falls_back_to_duration()
    {
        var inRange = new LyricsCandidate("1", "歌名", "", "", TimeSpan.FromSeconds(201));
        var tooFar = new LyricsCandidate("2", "歌名", "", "", TimeSpan.FromSeconds(260));

        Assert.True(LyricsMatcher.IsTrustworthy(inRange, Query(artist: "")));
        Assert.False(LyricsMatcher.IsTrustworthy(tooFar, Query(artist: "")));
    }

    [Fact]
    public void Pick_returns_null_when_nothing_is_trustworthy()
    {
        var candidates = new List<LyricsCandidate>
        {
            new("1", "别的歌", "歌手", "", TimeSpan.FromSeconds(200)),
            new("2", "歌名", "别人", "", TimeSpan.FromSeconds(200)),
        };

        Assert.Null(LyricsMatcher.Pick(candidates, Query()));
    }
}

/// <summary>网易云源：搜索 → 认领 → 取词；接口异常/无歌词一律降级为 null。</summary>
public class NeteaseProviderTests
{
    private static readonly LyricsQuery Query = new("灰すら遺らなくても", "Canaxi", "", TimeSpan.FromSeconds(224));

    [Fact]
    public async Task Fetch_searches_then_reads_the_lyric()
    {
        var handler = new StubHandler(url => url.Contains("/api/search")
            ? Json("""
                   {"result":{"songs":[
                     {"id":2704009961,"name":"灰すら遺らなくても。","duration":224640,
                      "artists":[{"name":"Canaxi"}],"album":{"name":"专辑"}}]}}
                   """)
            : Json("""{"lrc":{"lyric":"[00:01.00]日文第一句"},"tlyric":{"lyric":"[00:01.00]翻译"}}"""));

        var timeline = await new NeteaseProvider(new HttpClient(handler)).FetchAsync(Query, CancellationToken.None);

        Assert.NotNull(timeline);
        Assert.Equal("日文第一句", timeline!.CurrentAt(TimeSpan.FromSeconds(2)));
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("s=", handler.Requests[0]);          // 先搜索
        Assert.Contains("/api/song/lyric", handler.Requests[1]);
        Assert.Contains("id=2704009961", handler.Requests[1]);   // 用的是搜索命中的 id
    }

    [Fact]
    public async Task Fetch_rejects_a_search_hit_by_another_artist()
    {
        var handler = new StubHandler(url => url.Contains("/api/search")
            ? Json("""
                   {"result":{"songs":[
                     {"id":1,"name":"灰すら遺らなくても。","duration":224640,
                      "artists":[{"name":"别人"}],"album":{"name":"专辑"}}]}}
                   """)
            : Json("""{"lrc":{"lyric":"[00:01.00]不该被取到"}}"""));

        Assert.Null(await new NeteaseProvider(new HttpClient(handler)).FetchAsync(Query, CancellationToken.None));
        Assert.DoesNotContain("/api/song/lyric", handler.Requests);   // 认领失败 → 根本不去取词
    }

    [Fact]
    public async Task Fetch_returns_null_when_the_api_has_no_lyric()
    {
        var handler = new StubHandler(url => url.Contains("/api/search")
            ? Json("""{"result":{"songs":[]}}""")
            : Json("""{"lrc":{"lyric":""}}"""));

        Assert.Null(await new NeteaseProvider(new HttpClient(handler)).FetchAsync(Query, CancellationToken.None));
    }

    [Fact]
    public async Task Fetch_degrades_silently_on_http_failure()
    {
        var handler = new StubHandler(_ => Json("{}", HttpStatusCode.TooManyRequests));

        Assert.Null(await new NeteaseProvider(new HttpClient(handler)).FetchAsync(Query, CancellationToken.None));
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<string, HttpResponseMessage> _respond;

        public StubHandler(Func<string, HttpResponseMessage> respond) => _respond = respond;

        public List<string> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();
            Requests.Add(url);
            return Task.FromResult(_respond(url));
        }
    }
}

/// <summary>
/// 多源**并行择优**：两个源都会被问到，汇总后取最像的那一条；同分才偏向列表靠前（= 偏好源）；
/// 「纯音乐，请欣赏」这类占位一律丢弃。
/// </summary>
public class MergedLyricsProviderTests
{
    private static readonly LyricsQuery Query = new("歌名", "歌手", "专辑", TimeSpan.FromSeconds(200));

    private static LrcTimeline Timeline(string text) => LrcParser.Parse("[00:01.00]" + text);

    private static LyricsHit Hit(string text) =>
        new(new LyricsCandidate("id", Query.Title, Query.Artist, Query.Album, Query.Duration), Timeline(text));

    private static LyricsHit Hit(string text, string title, string artist, double seconds) =>
        new(new LyricsCandidate("id", title, artist, Query.Album, TimeSpan.FromSeconds(seconds)), Timeline(text));

    [Fact]
    public async Task Probes_every_source_even_after_a_hit()
    {
        var first = new FakeSource("a", _ => Task.FromResult<LyricsHit?>(Hit("第一个源")));
        var second = new FakeSource("b", _ => Task.FromResult<LyricsHit?>(Hit("第二个源")));

        await new MergedLyricsProvider(first, second).FetchAsync(Query, CancellationToken.None);

        // 顺序回退时代第二个源根本不会被问到 —— 这正是本次改造的核心
        Assert.Equal(1, first.Calls);
        Assert.Equal(1, second.Calls);
    }

    [Fact]
    public async Task Higher_scoring_candidate_beats_the_preferred_source()
    {
        // 两个候选都可信，但后者时长与查询完全吻合 → 分更高，应由它胜出（偏好不是无条件偏向）
        var preferred = new FakeSource("preferred", _ => Task.FromResult<LyricsHit?>(
            Hit("偏好源", Query.Title, Query.Artist, 205d)));
        var other = new FakeSource("other", _ => Task.FromResult<LyricsHit?>(
            Hit("更准的源", Query.Title, Query.Artist, 200d)));

        var timeline = await new MergedLyricsProvider(preferred, other).FetchAsync(Query, CancellationToken.None);

        Assert.Equal("更准的源", timeline!.CurrentAt(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public async Task Preference_breaks_ties()
    {
        var preferred = new FakeSource("preferred", _ => Task.FromResult<LyricsHit?>(Hit("偏好源")));
        var other = new FakeSource("other", _ => Task.FromResult<LyricsHit?>(Hit("另一源")));

        var timeline = await new MergedLyricsProvider(preferred, other).FetchAsync(Query, CancellationToken.None);

        Assert.Equal("偏好源", timeline!.CurrentAt(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public async Task Instrumental_filler_loses_to_real_lyrics()
    {
        var filler = new FakeSource("filler", _ => Task.FromResult<LyricsHit?>(Hit("纯音乐，请欣赏")));
        var real = new FakeSource("real", _ => Task.FromResult<LyricsHit?>(Hit("真正的歌词")));

        var timeline = await new MergedLyricsProvider(filler, real).FetchAsync(Query, CancellationToken.None);

        Assert.Equal("真正的歌词", timeline!.CurrentAt(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public async Task Filler_only_returns_null()
    {
        var merged = new MergedLyricsProvider(
            new FakeSource("a", _ => Task.FromResult<LyricsHit?>(Hit("纯音乐，请欣赏"))),
            new FakeSource("b", _ => Task.FromResult<LyricsHit?>(null)));

        Assert.Null(await merged.FetchAsync(Query, CancellationToken.None));
    }

    [Fact]
    public async Task A_throwing_source_does_not_break_the_merge()
    {
        var broken = new FakeSource("broken", _ => throw new InvalidOperationException("接口炸了"));
        var hit = new FakeSource("hit", _ => Task.FromResult<LyricsHit?>(Hit("后续源")));

        var timeline = await new MergedLyricsProvider(broken, hit).FetchAsync(Query, CancellationToken.None);

        Assert.Equal("后续源", timeline!.CurrentAt(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public async Task A_same_title_with_another_artist_is_rejected()
    {
        // 曲名一样、歌手是别人 → 那是别人的歌，宁可不显示也不能张冠李戴
        var wrong = new FakeSource("wrong", _ => Task.FromResult<LyricsHit?>(
            Hit("别人的歌", Query.Title, "另一个人", 200d)));

        Assert.Null(await new MergedLyricsProvider(wrong).FetchAsync(Query, CancellationToken.None));
    }

    [Fact]
    public async Task All_miss_returns_null()
    {
        var merged = new MergedLyricsProvider(
            new FakeSource("a", _ => Task.FromResult<LyricsHit?>(null)),
            new FakeSource("b", _ => Task.FromResult<LyricsHit?>(null)));

        Assert.Null(await merged.FetchAsync(Query, CancellationToken.None));
    }

    private sealed class FakeSource : ICandidateLyricsProvider
    {
        private readonly Func<LyricsQuery, Task<LyricsHit?>> _handler;

        public FakeSource(string name, Func<LyricsQuery, Task<LyricsHit?>> handler)
        {
            Name = name;
            _handler = handler;
        }

        public string Name { get; }

        public int Calls { get; private set; }

        public Task<LyricsHit?> LookupAsync(LyricsQuery query, CancellationToken ct)
        {
            Calls++;
            return _handler(query);
        }

        public async Task<LrcTimeline?> FetchAsync(LyricsQuery query, CancellationToken ct) =>
            (await LookupAsync(query, ct))?.Timeline;
    }
}

/// <summary>「偏好 X」= 串行回退：第一个给出歌词的就用，后面的不再问（不比较谁更像）。</summary>
public class FallbackLyricsProviderTests
{
    private static readonly LyricsQuery Query = new("歌名", "歌手", "专辑", TimeSpan.FromSeconds(200));

    private static LrcTimeline Timeline(string text) => LrcParser.Parse("[00:01.00]" + text);

    [Fact]
    public async Task First_hit_wins_and_the_rest_are_not_asked()
    {
        var first = new CountingProvider(_ => Task.FromResult<LrcTimeline?>(Timeline("排第一的源")));
        var later = new CountingProvider(_ => Task.FromResult<LrcTimeline?>(Timeline("不该被问到")));

        var timeline = await new FallbackLyricsProvider(first, later).FetchAsync(Query, CancellationToken.None);

        Assert.Equal("排第一的源", timeline!.CurrentAt(TimeSpan.FromSeconds(2)));
        Assert.Equal(1, first.Calls);
        Assert.Equal(0, later.Calls);
    }

    [Fact]
    public async Task Falls_through_when_the_preferred_source_misses()
    {
        var miss = new CountingProvider(_ => Task.FromResult<LrcTimeline?>(null));
        var next = new CountingProvider(_ => Task.FromResult<LrcTimeline?>(Timeline("下一个源")));

        var timeline = await new FallbackLyricsProvider(miss, next).FetchAsync(Query, CancellationToken.None);

        Assert.Equal("下一个源", timeline!.CurrentAt(TimeSpan.FromSeconds(2)));
        Assert.Equal(1, miss.Calls);
        Assert.Equal(1, next.Calls);
    }

    [Fact]
    public async Task A_throwing_source_does_not_break_the_chain()
    {
        var broken = new CountingProvider(_ => throw new InvalidOperationException("接口炸了"));
        var hit = new CountingProvider(_ => Task.FromResult<LrcTimeline?>(Timeline("后续源")));

        var timeline = await new FallbackLyricsProvider(broken, hit).FetchAsync(Query, CancellationToken.None);

        Assert.Equal("后续源", timeline!.CurrentAt(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public async Task All_miss_returns_null()
    {
        var chain = new FallbackLyricsProvider(
            new CountingProvider(_ => Task.FromResult<LrcTimeline?>(null)),
            new CountingProvider(_ => Task.FromResult<LrcTimeline?>(null)));

        Assert.Null(await chain.FetchAsync(Query, CancellationToken.None));
    }

    private sealed class CountingProvider : ILyricsProvider
    {
        private readonly Func<LyricsQuery, Task<LrcTimeline?>> _handler;

        public CountingProvider(Func<LyricsQuery, Task<LrcTimeline?>> handler) => _handler = handler;

        public int Calls { get; private set; }

        public Task<LrcTimeline?> FetchAsync(LyricsQuery query, CancellationToken ct)
        {
            Calls++;
            return _handler(query);
        }
    }
}

/// <summary>「纯音乐，请欣赏」这类占位歌词的识别（占位不是歌词，不能显示）。</summary>
public class InstrumentalFillerTests
{
    [Theory]
    [InlineData("纯音乐，请欣赏")]
    [InlineData("纯音乐")]
    [InlineData("该歌曲为纯音乐，请欣赏")]
    [InlineData("Instrumental")]
    [InlineData("此歌曲为没有填词的纯音乐")]
    [InlineData("暂无歌词")]
    public void Filler_is_detected(string text)
    {
        Assert.True(LyricsMatcher.IsInstrumentalFiller(LrcParser.Parse("[00:01.00]" + text)));
    }

    [Fact]
    public void Real_lyrics_are_not_filler()
    {
        var timeline = LrcParser.Parse("[00:01.00]第一句\n[00:05.00]第二句\n[00:09.00]第三句\n[00:13.00]第四句");
        Assert.False(LyricsMatcher.IsInstrumentalFiller(timeline));
    }

    [Fact]
    public void A_long_lyric_that_mentions_the_word_is_not_filler()
    {
        // 真歌词里出现「纯音乐」三个字不该被误杀（行数闸门挡住）
        var timeline = LrcParser.Parse(
            "[00:01.00]纯音乐也很好听\n[00:05.00]第二句\n[00:09.00]第三句\n[00:13.00]第四句");
        Assert.False(LyricsMatcher.IsInstrumentalFiller(timeline));
    }

    [Fact]
    public void Empty_timeline_is_not_filler() =>
        Assert.False(LyricsMatcher.IsInstrumentalFiller(LrcTimeline.Empty));
}
