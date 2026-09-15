using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EndfieldCharge.Host.Plugins.Music;
using Xunit;

namespace EndfieldCharge.Tests;

/// <summary>
/// 歌词磁盘缓存：LRU 淘汰、清空、用户文件安全边界、过期文件清理。
/// <para>
/// 缓存目录与用户的本地歌词共用（<see cref="LocalLrcProvider"/> 也读这里），所以删除的
/// 安全边界是重点：只允许碰 <c>^[0-9A-F]{16}\.lrc$</c>。全部用临时目录，不写 %APPDATA%。
/// </para>
/// </summary>
public class LyricsCacheTests
{
    private const string UserFile = "周杰伦 - 晴天.lrc";

    private static readonly LyricsQuery Query = new("歌名", "歌手", "专辑", TimeSpan.FromSeconds(215));

    // ---------------- LyricsCache 纯逻辑 ----------------

    [Fact]
    public void Prune_evicts_oldest_first_and_stops_at_limit()
    {
        using var dir = new TempDir();
        var oldest = WriteCacheFile(dir.Path, CacheName(1), 1000, DateTime.UtcNow.AddMinutes(-30));
        var middle = WriteCacheFile(dir.Path, CacheName(2), 1000, DateTime.UtcNow.AddMinutes(-20));
        var newest = WriteCacheFile(dir.Path, CacheName(3), 1000, DateTime.UtcNow.AddMinutes(-10));

        var (deleted, freed) = LyricsCache.Prune(dir.Path, 1500);

        Assert.Equal(2, deleted);
        Assert.Equal(2000L, freed);
        Assert.False(File.Exists(oldest));      // 最久未用先走
        Assert.False(File.Exists(middle));
        Assert.True(File.Exists(newest));

        var (files, bytes) = LyricsCache.Measure(dir.Path);
        Assert.Equal(1, files);
        Assert.Equal(1000L, bytes);
        Assert.True(bytes <= 1500);
    }

    [Fact]
    public void Prune_and_clear_never_touch_user_or_foreign_files()
    {
        using var dir = new TempDir();
        var user = Path.Combine(dir.Path, UserFile);
        File.WriteAllText(user, "[00:01.00]手写的本地歌词");
        var hexButNotLrc = Path.Combine(dir.Path, "0123456789ABCDEF.txt");
        var shortHex = Path.Combine(dir.Path, "0123456789ABCDE.lrc");     // 15 位：不是缓存命名
        var notHex = Path.Combine(dir.Path, "XYZ - 歌名.lrc");
        File.WriteAllText(hexButNotLrc, "x");
        File.WriteAllText(shortHex, "[00:01.00]短名歌词");
        File.WriteAllText(notHex, "[00:01.00]非哈希名歌词");

        // 缓存文件远超上限（上限比单个文件还小），逼着淘汰把缓存全删光
        for (int i = 0; i < 20; i++)
            WriteCacheFile(dir.Path, CacheName(i), 4096, DateTime.UtcNow.AddMinutes(-i));

        var (deleted, freed) = LyricsCache.Prune(dir.Path, 1024);

        Assert.Equal(20, deleted);
        Assert.Equal(20L * 4096, freed);
        Assert.Equal("[00:01.00]手写的本地歌词", File.ReadAllText(user));
        Assert.True(File.Exists(hexButNotLrc));
        Assert.True(File.Exists(shortHex));
        Assert.True(File.Exists(notHex));
        Assert.Empty(CacheFiles(dir.Path));

        // 清空同样只碰缓存文件
        WriteCacheFile(dir.Path, CacheName(99), 100, DateTime.UtcNow);
        var (cleared, clearedBytes) = LyricsCache.Clear(dir.Path);

        Assert.Equal(1, cleared);
        Assert.Equal(100L, clearedBytes);
        Assert.True(File.Exists(user));
        Assert.True(File.Exists(shortHex));
        Assert.Empty(CacheFiles(dir.Path));
    }

    [Fact]
    public void Clear_removes_cache_files_only()
    {
        using var dir = new TempDir();
        var user = Path.Combine(dir.Path, UserFile);
        File.WriteAllText(user, "[00:01.00]保留");
        WriteCacheFile(dir.Path, CacheName(1), 512, DateTime.UtcNow);
        WriteCacheFile(dir.Path, CacheName(2), 1536, DateTime.UtcNow);

        var (deleted, freed) = LyricsCache.Clear(dir.Path);

        Assert.Equal(2, deleted);
        Assert.Equal(2048L, freed);
        Assert.Empty(CacheFiles(dir.Path));
        Assert.True(File.Exists(user));
        Assert.Equal((0, 0L), LyricsCache.Measure(dir.Path));
    }

    [Fact]
    public void Measure_prune_and_clear_are_noops_on_missing_or_empty_directory()
    {
        using var dir = new TempDir();

        Assert.Equal((0, 0L), LyricsCache.Measure(dir.Path));       // 空目录
        Assert.Equal((0, 0L), LyricsCache.Prune(dir.Path, 0));
        Assert.Equal((0, 0L), LyricsCache.Clear(dir.Path));

        var missing = Path.Combine(dir.Path, "missing");
        Assert.Equal((0, 0L), LyricsCache.Measure(missing));
        Assert.Equal((0, 0L), LyricsCache.Prune(missing, 1024));
        Assert.Equal((0, 0L), LyricsCache.Clear(missing));

        Assert.Equal((0, 0L), LyricsCache.Measure(string.Empty));
        Assert.Equal((0, 0L), LyricsCache.Prune(string.Empty, 1024));
        Assert.Equal((0, 0L), LyricsCache.Clear(string.Empty));
    }

    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(999, "999 B")]
    [InlineData(1024, "1.0 KB")]
    [InlineData(312 * 1024, "312 KB")]
    [InlineData(1024 * 1024, "1.0 MB")]
    [InlineData(4404019, "4.2 MB")]
    [InlineData(200L * 1024 * 1024, "200 MB")]
    public void FormatSize_is_human_readable(long bytes, string expected) =>
        Assert.Equal(expected, LyricsCache.FormatSize(bytes));

    [Theory]
    [InlineData("0123456789ABCDEF.lrc", true)]
    [InlineData("0123456789abcdef.lrc", true)]      // 大小写不敏感
    [InlineData("0123456789ABCDEF.LRC", true)]
    [InlineData("0123456789ABCDE.lrc", false)]      // 15 位
    [InlineData("0123456789ABCDEF0.lrc", false)]    // 17 位
    [InlineData("0123456789ABCDEF.txt", false)]
    [InlineData("周杰伦 - 晴天.lrc", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsCacheFile_accepts_only_hashed_names(string? name, bool expected) =>
        Assert.Equal(expected, LyricsCache.IsCacheFile(name));

    [Theory]
    [InlineData(0, 5)]
    [InlineData(4, 5)]
    [InlineData(20, 20)]
    [InlineData(201, 200)]
    public void ClampLimitMegabytes_keeps_the_used_value_inside_5_to_200(int value, int expected) =>
        Assert.Equal(expected, LyricsCache.ClampLimitMegabytes(value));

    // ---------------- LrclibProvider 集成（缓存目录安全 + LRU） ----------------

    [Fact]
    public void Provider_prunes_on_construction_and_spares_user_files()
    {
        using var dir = new TempDir();
        var user = Path.Combine(dir.Path, UserFile);
        File.WriteAllText(user, "[00:01.00]手写的本地歌词");
        WriteCacheFile(dir.Path, CacheName(1), 4000, DateTime.UtcNow.AddMinutes(-30));
        WriteCacheFile(dir.Path, CacheName(2), 4000, DateTime.UtcNow.AddMinutes(-20));
        var newest = WriteCacheFile(dir.Path, CacheName(3), 4000, DateTime.UtcNow.AddMinutes(-10));

        _ = new LrclibProvider(dir.Path, cacheLimitBytes: 5000);

        Assert.True(File.Exists(newest));                       // 只留最新的一条
        Assert.Single(CacheFiles(dir.Path));
        Assert.True(LyricsCache.Measure(dir.Path).Bytes <= 5000);
        Assert.True(File.Exists(user));                         // 用户手写的歌词不受影响
    }

    [Fact]
    public async Task Provider_prunes_after_write_when_the_new_entry_exceeds_the_limit()
    {
        using var dir = new TempDir();
        var lyric = "[00:01.000]" + new string('x', 2000);
        var handler = new StubHandler(_ => Json($"{{\"syncedLyrics\":\"{lyric}\"}}"));

        var first = new LrclibProvider(dir.Path, new HttpClient(handler));
        await first.FetchAsync(Query with { Title = "歌甲" }, CancellationToken.None);
        long oneFile = LyricsCache.Measure(dir.Path).Bytes;
        Assert.True(oneFile > 0);

        // 上限只够一个文件：第二次写入后必须回收最久未用的那条
        var tight = new LrclibProvider(dir.Path, new HttpClient(handler), oneFile + 10);
        await tight.FetchAsync(Query with { Title = "歌乙" }, CancellationToken.None);

        Assert.Equal(2, handler.Requests.Count);                // 两首都真的取了（不是命中）
        var cacheFile = Assert.Single(CacheFiles(dir.Path));
        Assert.Contains("歌乙", File.ReadAllText(cacheFile));    // 留的是最新写入的那条
        Assert.True(LyricsCache.Measure(dir.Path).Bytes <= oneFile + 10);
    }

    [Fact]
    public async Task Provider_deletes_cache_file_without_candidate_header()
    {
        using var dir = new TempDir();
        var hit = new StubHandler(_ => Json("""{"syncedLyrics":"[00:01.00]会被改坏的缓存"}"""));
        await Provider(hit, dir.Path).FetchAsync(Query, CancellationToken.None);

        var cacheFile = Assert.Single(CacheFiles(dir.Path));
        File.WriteAllText(cacheFile, "[00:01.00]旧格式：没有候选头");   // 模拟旧版本写的缓存

        var miss = new StubHandler(_ => Json("{}", HttpStatusCode.NotFound));
        Assert.Null(await Provider(miss, dir.Path).FetchAsync(Query, CancellationToken.None));

        Assert.False(File.Exists(cacheFile));                   // 过期文件被清掉，不再无限留着
    }

    [Fact]
    public async Task Provider_touches_cache_file_on_hit_so_lru_reflects_last_use()
    {
        using var dir = new TempDir();
        var hit = new StubHandler(_ => Json("""{"syncedLyrics":"[00:01.00]第一句"}"""));
        await Provider(hit, dir.Path).FetchAsync(Query, CancellationToken.None);

        var cacheFile = Assert.Single(CacheFiles(dir.Path));
        File.SetLastWriteTimeUtc(cacheFile, DateTime.UtcNow.AddDays(-3));

        var second = new StubHandler(_ => Json("{}", HttpStatusCode.NotFound));
        Assert.NotNull(await Provider(second, dir.Path).FetchAsync(Query, CancellationToken.None));

        Assert.Empty(second.Requests);                          // 磁盘命中 → 不联网
        Assert.True(File.GetLastWriteTimeUtc(cacheFile) > DateTime.UtcNow.AddDays(-1));
    }

    // ---------------- 测试替身 ----------------

    /// <summary>16 位十六进制 + <c>.lrc</c>：与 <c>LrclibProvider.CachePath</c> 的命名形状一致。</summary>
    private static string CacheName(int index) =>
        index.ToString("X16", CultureInfo.InvariantCulture) + ".lrc";

    private static string WriteCacheFile(string directory, string name, int bytes, DateTime lastWriteUtc)
    {
        var path = Path.Combine(directory, name);
        File.WriteAllText(path, new string('x', bytes));
        File.SetLastWriteTimeUtc(path, lastWriteUtc);
        return path;
    }

    private static string[] CacheFiles(string directory) =>
        Directory.GetFiles(directory, "*.lrc")
            .Where(path => LyricsCache.IsCacheFile(Path.GetFileName(path)))
            .ToArray();

    private static LrclibProvider Provider(StubHandler handler, string cacheDirectory) =>
        new(cacheDirectory, new HttpClient(handler));

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<string, HttpResponseMessage> _respond;

        public StubHandler(Func<string, HttpResponseMessage> respond) => _respond = respond;

        public List<string> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!.ToString());
            return Task.FromResult(_respond(request.RequestUri!.ToString()));
        }
    }

    private sealed class TempDir : IDisposable
    {
        public TempDir() => Directory.CreateDirectory(Path);

        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "ec-lyrics-cache-" + Guid.NewGuid().ToString("N"));

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
