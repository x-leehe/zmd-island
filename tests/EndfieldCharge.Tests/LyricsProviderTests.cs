using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EndfieldCharge.Host.Plugins.Music;
using Xunit;

namespace EndfieldCharge.Tests;

/// <summary>
/// 取词层：端点选择、JSON 映射、回退、磁盘缓存、失败降级。
/// 全部用桩 <see cref="HttpMessageHandler"/>，不触网。
/// </summary>
public class LyricsProviderTests
{
    private static readonly LyricsQuery Query = new("歌名", "歌手", "专辑", TimeSpan.FromSeconds(215));

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static LrclibProvider Provider(StubHandler handler, string cacheDirectory)
        => new(cacheDirectory, new HttpClient(handler));

    [Fact]
    public async Task Fetch_uses_exact_endpoint_and_reads_synced_lyrics()
    {
        using var dir = new TempDir();
        var handler = new StubHandler(_ => Json("""{"syncedLyrics":"[00:01.00]第一句"}"""));

        var timeline = await Provider(handler, dir.Path).FetchAsync(Query, CancellationToken.None);

        Assert.NotNull(timeline);
        Assert.Equal("第一句", timeline!.CurrentAt(TimeSpan.FromSeconds(2)));

        var url = Assert.Single(handler.Requests);
        Assert.Contains("/api/get", url);
        Assert.Contains("track_name=", url);
        Assert.Contains("artist_name=", url);
        Assert.Contains("duration=215", url);
    }

    [Fact]
    public async Task Fetch_ignores_plain_lyrics_without_timestamps()
    {
        using var dir = new TempDir();
        var handler = new StubHandler(_ => Json("""{"plainLyrics":"没有时间轴的纯文本"}"""));

        Assert.Null(await Provider(handler, dir.Path).FetchAsync(Query, CancellationToken.None));
    }

    [Fact]
    public async Task Fetch_falls_back_to_search_when_exact_lookup_misses()
    {
        using var dir = new TempDir();
        var handler = new StubHandler(url => url.Contains("/api/get")
            ? Json("""{"code":404,"message":"not found"}""", HttpStatusCode.NotFound)
            : Json("""[{"instrumental":false},{"trackName":"歌名","artistName":"歌手","duration":215,"syncedLyrics":"[00:02.00]来自搜索"}]"""));

        var timeline = await Provider(handler, dir.Path).FetchAsync(Query, CancellationToken.None);

        Assert.NotNull(timeline);
        Assert.Equal("来自搜索", timeline!.CurrentAt(TimeSpan.FromSeconds(2)));
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("/api/search", handler.Requests[1]);
    }

    [Fact]
    public async Task Fetch_skips_instrumental_tracks()
    {
        using var dir = new TempDir();
        var handler = new StubHandler(_ => Json("""{"instrumental":true,"syncedLyrics":"[00:01.00]不该用"}"""));

        Assert.Null(await Provider(handler, dir.Path).FetchAsync(Query, CancellationToken.None));
    }

    [Fact]
    public async Task Fetch_does_nothing_without_a_title()
    {
        using var dir = new TempDir();
        var handler = new StubHandler(_ => Json("{}"));
        var empty = new LyricsQuery(string.Empty, "歌手", "专辑", TimeSpan.FromSeconds(10));

        Assert.Null(await Provider(handler, dir.Path).FetchAsync(empty, CancellationToken.None));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Fetch_reuses_disk_cache_across_instances()
    {
        using var dir = new TempDir();
        var first = new StubHandler(_ => Json("""{"syncedLyrics":"[00:01.00]第一次"}"""));
        await Provider(first, dir.Path).FetchAsync(Query, CancellationToken.None);

        var second = new StubHandler(_ => Json("""{"syncedLyrics":"[00:09.00]不该被用到"}"""));
        var timeline = await Provider(second, dir.Path).FetchAsync(Query, CancellationToken.None);

        Assert.Empty(second.Requests);                     // 磁盘命中 → 不联网
        Assert.Equal("第一次", timeline!.CurrentAt(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public async Task Fetch_remembers_failure_and_stops_retrying()
    {
        using var dir = new TempDir();
        var handler = new StubHandler(_ => Json("{}", HttpStatusCode.ServiceUnavailable));
        var provider = Provider(handler, dir.Path);

        Assert.Null(await provider.FetchAsync(Query, CancellationToken.None));
        int afterFirst = handler.Requests.Count;

        Assert.Null(await provider.FetchAsync(Query, CancellationToken.None));
        Assert.Equal(afterFirst, handler.Requests.Count);   // 失败也进内存缓存，避免每 tick 重打网络
    }

    [Fact]
    public void Query_key_is_stable_and_duration_aware()
    {
        var a = new LyricsQuery("歌名", "歌手", "专辑", TimeSpan.FromSeconds(215.4));
        var b = new LyricsQuery("歌名", "歌手", "专辑", TimeSpan.FromSeconds(214.6));
        var other = new LyricsQuery("歌名", "歌手", "专辑", TimeSpan.FromSeconds(300));

        Assert.Equal(a.Key, b.Key);          // 都取整到 215 秒 → 同一首
        Assert.NotEqual(a.Key, other.Key);   // 长度差得多 → 不是同一首
    }

    [Fact]
    public async Task Local_provider_matches_artist_dash_title_file()
    {
        using var dir = new TempDir();
        File.WriteAllText(Path.Combine(dir.Path, "歌手 - 歌名.lrc"), "[00:03.00]本地歌词");

        var timeline = await new LocalLrcProvider(dir.Path).FetchAsync(Query, CancellationToken.None);

        Assert.NotNull(timeline);
        Assert.Equal("本地歌词", timeline!.CurrentAt(TimeSpan.FromSeconds(3)));
    }

    [Fact]
    public async Task Local_provider_falls_back_to_title_only_and_sanitizes_names()
    {
        using var dir = new TempDir();
        File.WriteAllText(Path.Combine(dir.Path, "独奏.lrc"), "[00:01.00]只有曲名");
        var titleOnly = new LyricsQuery("独奏", "未知", string.Empty, TimeSpan.Zero);

        var first = await new LocalLrcProvider(dir.Path).FetchAsync(titleOnly, CancellationToken.None);
        Assert.Equal("只有曲名", first!.CurrentAt(TimeSpan.FromSeconds(1)));

        // 非法文件名字符被替换成下划线后仍可命中
        File.WriteAllText(Path.Combine(dir.Path, "C_D - A_B.lrc"), "[00:02.00]替换后命中");
        var weird = new LyricsQuery("A/B", "C:D", string.Empty, TimeSpan.Zero);
        var second = await new LocalLrcProvider(dir.Path).FetchAsync(weird, CancellationToken.None);
        Assert.Equal("替换后命中", second!.CurrentAt(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public async Task Local_provider_returns_null_when_directory_is_empty()
    {
        using var dir = new TempDir();

        Assert.Null(await new LocalLrcProvider(dir.Path).FetchAsync(Query, CancellationToken.None));
        Assert.Null(await new LocalLrcProvider(string.Empty).FetchAsync(Query, CancellationToken.None));
    }

    [Fact]
    public async Task Fetch_search_falls_back_to_title_only_for_messy_artist_names()
    {
        using var dir = new TempDir();
        // 艺术家是多人拼接 + 带 remix 后缀：带 artist_name 的搜索必然 miss，只有纯曲名搜得到
        var handler = new StubHandler(url => url.Contains("/api/get")
            ? Json("{}", HttpStatusCode.NotFound)
            : url.Contains("artist_name=")
                ? Json("[]")
                : Json("""[{"trackName":"歌名","artistName":"歌手","duration":215,"syncedLyrics":"[00:04.00]纯曲名搜到的"}]"""));

        var timeline = await Provider(handler, dir.Path).FetchAsync(Query, CancellationToken.None);

        Assert.NotNull(timeline);
        Assert.Equal("纯曲名搜到的", timeline!.CurrentAt(TimeSpan.FromSeconds(5)));
        Assert.Equal(3, handler.Requests.Count);                       // get → search(曲名+艺术家) → search(仅曲名)
        Assert.DoesNotContain("artist_name=", handler.Requests[^1]);   // 最后一条不再带艺术家
    }

    [Fact]
    public async Task Fetch_search_strips_remix_suffix_from_the_title()
    {
        using var dir = new TempDir();
        var messy = new LyricsQuery("Falling into you （Sped up）", "TIC,Paperman", "专辑", TimeSpan.FromSeconds(215));
        var handler = new StubHandler(url =>
            url.Contains("/api/get") ? Json("{}", HttpStatusCode.NotFound)
            : url.Contains("Sped") ? Json("[]")                        // 带后缀的两级都搜不到
            : Json("""[{"trackName":"Falling into you","artistName":"TIC","duration":216,"syncedLyrics":"[00:03.00]剥掉后缀后命中"}]"""));

        var timeline = await Provider(handler, dir.Path).FetchAsync(messy, CancellationToken.None);

        Assert.NotNull(timeline);
        Assert.Equal("剥掉后缀后命中", timeline!.CurrentAt(TimeSpan.FromSeconds(4)));
        Assert.Equal(4, handler.Requests.Count);
    }

    [Fact]
    public async Task Fetch_search_rejects_same_title_by_another_artist_even_when_duration_matches()
    {
        using var dir = new TempDir();
        // 实测踩过的坑：曲名一样、时长也接近，但根本不是同一个人的歌
        // → 必须拒绝：错的歌词比没有歌词更糟
        var handler = new StubHandler(url => url.Contains("/api/get")
            ? Json("{}", HttpStatusCode.NotFound)
            : Json("""
                   [{"trackName":"Falling into you （Sped up）","artistName":"Brighter Than A Thousand Suns",
                     "duration":215,"syncedLyrics":"[00:02.00]别人的歌词"}]
                   """));
        var messy = new LyricsQuery(
            "Falling into you （Sped up）", "TIC,Paperman", string.Empty, TimeSpan.FromSeconds(215));

        Assert.Null(await Provider(handler, dir.Path).FetchAsync(messy, CancellationToken.None));
    }

    [Fact]
    public async Task Fetch_search_accepts_only_candidates_with_a_matching_title()
    {
        using var dir = new TempDir();
        var handler = new StubHandler(url => url.Contains("/api/get")
            ? Json("{}", HttpStatusCode.NotFound)
            : Json("""
                   [{"trackName":"完全别的歌","artistName":"歌手","duration":215,"syncedLyrics":"[00:01.00]不该用"},
                    {"trackName":"歌名 （Sped up）","artistName":"歌手","duration":216,"syncedLyrics":"[00:06.00]应该用"}]
                   """));

        // 第二条：曲名去掉 (Sped up) 后与查询一致 —— 版本后缀差异不算"不同的歌"
        var timeline = await Provider(handler, dir.Path).FetchAsync(Query, CancellationToken.None);

        Assert.NotNull(timeline);
        Assert.Equal("应该用", timeline!.CurrentAt(TimeSpan.FromSeconds(7)));
    }

    [Fact]
    public async Task Fetch_search_falls_back_to_duration_when_artist_is_unknown()
    {
        using var dir = new TempDir();
        var handler = new StubHandler(url => url.Contains("/api/get")
            ? Json("{}", HttpStatusCode.NotFound)
            : Json("""[{"trackName":"歌名","duration":215,"syncedLyrics":"[00:01.00]靠时长认领"}]"""));

        // 艺术家未知（SMTC 有时给不出）→ 退而按时长认领
        var noArtist = new LyricsQuery("歌名", string.Empty, string.Empty, TimeSpan.FromSeconds(215));
        var timeline = await Provider(handler, dir.Path).FetchAsync(noArtist, CancellationToken.None);

        Assert.Equal("靠时长认领", timeline!.CurrentAt(TimeSpan.FromSeconds(2)));
    }

    // ---------------- 测试替身 ----------------

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

    private sealed class TempDir : IDisposable
    {
        public TempDir() => Directory.CreateDirectory(Path);

        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "ec-lyrics-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, true);
            }
            catch
            {
                // 临时目录清理失败不影响测试结论
            }
        }
    }
}
