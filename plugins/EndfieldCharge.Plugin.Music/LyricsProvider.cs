using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EndfieldCharge.Services;

namespace EndfieldCharge.Host.Plugins.Music;

/// <summary>
/// 查歌词用的曲目指纹。
/// <para>
/// 时长参与匹配：LRCLIB 用它区分同名曲（同名同长的才是同一首），精度取整秒即可。
/// </para>
/// </summary>
public sealed record LyricsQuery(string Title, string Artist, string Album, TimeSpan Duration)
{
    /// <summary>缓存与去重用的稳定键（同名同长视为同一首）。</summary>
    public string Key => string.Join(
        '\u001f',
        Title.Trim(),
        Artist.Trim(),
        Album.Trim(),
        ((int)Math.Round(Duration.TotalSeconds)).ToString());

    /// <summary>指纹是否可用（至少有曲名）。</summary>
    public bool IsUsable => !string.IsNullOrWhiteSpace(Title);
}

/// <summary>歌词来源。实现不应抛异常：取不到就返回 <c>null</c>。</summary>
public interface ILyricsProvider
{
    /// <summary>取回可同步的歌词（LRC）；没有或失败时返回 <c>null</c>。</summary>
    Task<LrcTimeline?> FetchAsync(LyricsQuery query, CancellationToken ct);
}

/// <summary>
/// 一个来源的取词结果：**命中的候选** + 可直接显示的时间轴。
/// <para>
/// 多源汇总要靠候选信息来打分（"哪个源给的更像这首歌"），只回时间轴就无从比较。
/// </para>
/// </summary>
public sealed record LyricsHit(LyricsCandidate Candidate, LrcTimeline Timeline);

/// <summary>
/// 能「先搜后取」的来源：把搜索结果映射成候选、认领后再取词。
/// <para>
/// 多源汇总（<c>MergedLyricsProvider</c>）按此并行探测。只能给出唯一答案的来源
/// （如本地 .lrc 文件）不实现它 —— 那种来源各自就是一个独立的「来源选项」。
/// </para>
/// </summary>
public interface ICandidateLyricsProvider : ILyricsProvider
{
    /// <summary>来源名（日志与「偏好哪一源」的排序用）。</summary>
    string Name { get; }

    /// <summary>搜索 + 认领 + 取词；没有可信候选时返回 <c>null</c>。</summary>
    Task<LyricsHit?> LookupAsync(LyricsQuery query, CancellationToken ct);
}

/// <summary>
/// LRCLIB 在线歌词（<see href="https://lrclib.net"/>，公开接口、无需 API Key）。
/// <para>
/// 策略：先 <c>/api/get</c> 精确查询（艺术家 + 曲名 + 专辑 + 时长），命中失败再退
/// <c>/api/search</c>；只接受 <c>syncedLyrics</c>（纯文本歌词没有时间轴，在岛的一行位上一无用处）。
/// 结果落盘缓存，同一首不再联网。
/// </para>
/// </summary>
public sealed class LrclibProvider : ICandidateLyricsProvider
{
    private const string GetEndpoint = "https://lrclib.net/api/get";
    private const string SearchEndpoint = "https://lrclib.net/api/search";
    private const string UserAgent = "EndfieldIsland/1.0 (https://github.com/X-LeeHe/zmd-island)";

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(6);

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private static readonly Lazy<HttpClient> SharedClient = new(() =>
    {
        var client = new HttpClient { Timeout = RequestTimeout };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        return client;
    });

    private static readonly char[] ArtistsSeparators = { ',', '，', '&', '/', '、', ';', '；', '+', '|' };

    private readonly HttpClient _http;
    private readonly string _cacheDirectory;
    private readonly Dictionary<string, LyricsHit?> _memory = new(StringComparer.Ordinal);

    /// <summary>磁盘缓存上限（字节）：写入后与构造时按 LRU 回收；设置页改上限时由插件调 <see cref="ApplyCacheLimit"/> 更新。</summary>
    private long _cacheLimitBytes;

    /// <param name="cacheDirectory">磁盘缓存目录（<c>&lt;插件数据目录&gt;\lyrics</c>）；为空则只用内存缓存。</param>
    /// <param name="httpClient">测试可注入；为空时用共享实例。</param>
    /// <param name="cacheLimitBytes">磁盘缓存上限（字节）：构造时立即按它回收一次，超限先删最久未用的。</param>
    public LrclibProvider(
        string cacheDirectory,
        HttpClient? httpClient = null,
        long cacheLimitBytes = LyricsCache.DefaultLimitBytes)
    {
        _cacheDirectory = cacheDirectory ?? string.Empty;
        _http = httpClient ?? SharedClient.Value;
        ApplyCacheLimit(cacheLimitBytes);       // 上次运行留下的超限缓存，构造即回收
    }

    /// <summary>应用新的磁盘缓存上限（字节）并立即回收一次 —— 设置里调低上限后无需重启即生效。</summary>
    public void ApplyCacheLimit(long cacheLimitBytes)
    {
        _cacheLimitBytes = Math.Max(0, cacheLimitBytes);
        PruneCache();
    }

    /// <inheritdoc />
    public string Name => "LRCLIB";

    /// <inheritdoc />
    public async Task<LrcTimeline?> FetchAsync(LyricsQuery query, CancellationToken ct) =>
        (await LookupAsync(query, ct).ConfigureAwait(false))?.Timeline;

    /// <inheritdoc />
    public async Task<LyricsHit?> LookupAsync(LyricsQuery query, CancellationToken ct)
    {
        if (!query.IsUsable)
            return null;

        if (_memory.TryGetValue(query.Key, out var known))
            return known;

        var cached = ReadCache(query);
        if (cached is not null)
        {
            Logger.Info($"Music: 歌词命中（磁盘缓存 · LRCLIB · {Describe(query)} · {cached.Timeline.Lines.Count} 句）");
            _memory[query.Key] = cached;
            return cached;
        }

        var hit = await FetchExactAsync(query, ct).ConfigureAwait(false)
            ?? await FetchSearchAsync(query, ct).ConfigureAwait(false);

        if (hit is not null && hit.Timeline.IsEmpty)
            hit = null;

        // 命中与未命中都记一行：用户与支持据此确认"到底有没有去取、取到没有"
        if (hit is not null)
        {
            Logger.Info($"Music: 歌词命中（LRCLIB · {Describe(query)} · {hit.Timeline.Lines.Count} 句）");
            WriteCache(query, hit);
        }
        else
        {
            Logger.Info($"Music: LRCLIB 未找到可同步的歌词（{Describe(query)}）");
        }

        _memory[query.Key] = hit;
        return hit;
    }

    private static string Describe(LyricsQuery query) =>
        string.IsNullOrWhiteSpace(query.Artist) ? query.Title : $"{query.Artist} - {query.Title}";

    /// <summary>精确查询：四个字段都给，命中率最高。</summary>
    private async Task<LyricsHit?> FetchExactAsync(LyricsQuery query, CancellationToken ct)
    {
        var url = GetEndpoint
            + "?track_name=" + Uri.EscapeDataString(query.Title)
            + "&artist_name=" + Uri.EscapeDataString(query.Artist)
            + "&album_name=" + Uri.EscapeDataString(query.Album)
            + "&duration=" + ((int)Math.Round(query.Duration.TotalSeconds)).ToString();

        var payload = await GetJsonAsync(url, ct).ConfigureAwait(false);
        return payload is null ? null : ToHit(payload.Value, expectArray: false, query);
    }

    /// <summary>
    /// 回退搜索：先「曲名 + 艺术家」，再只按曲名，最后把曲名尾部的版本后缀（Sped up / Live…）剥掉再搜。
    /// <para>
    /// 候选**必须可信**才采用（见 <see cref="LyricsMatcher.IsTrustworthy"/>）：宁可不出歌词，也不要出错歌词 ——
    /// 实测踩过"同名不同歌手、时长还恰好接近"的坑。
    /// </para>
    /// </summary>
    private async Task<LyricsHit?> FetchSearchAsync(LyricsQuery query, CancellationToken ct)
    {
        foreach (var url in SearchUrls(query))
        {
            var payload = await GetJsonAsync(url, ct).ConfigureAwait(false);
            if (payload is null)
                continue;

            var hit = ToHit(payload.Value, expectArray: true, query);
            if (hit is not null)
                return hit;
        }

        return null;
    }

    private static IEnumerable<string> SearchUrls(LyricsQuery query)
    {
        yield return SearchEndpoint
            + "?track_name=" + Uri.EscapeDataString(query.Title)
            + "&artist_name=" + Uri.EscapeDataString(query.Artist);

        yield return SearchEndpoint
            + "?track_name=" + Uri.EscapeDataString(query.Title);

        // 曲名自己带后缀（(Sped up) / （Sped up） / [Live] …）时，前两级都搜不到：
        // 去掉后缀再搜一次，通常能命中原版，再由时长筛选决定用不用。
        var normalized = LyricsMatcher.NormalizeTitle(query.Title);
        if (!string.Equals(normalized, query.Title, StringComparison.Ordinal))
        {
            yield return SearchEndpoint
                + "?track_name=" + Uri.EscapeDataString(normalized);
        }
    }

    private async Task<JsonElement?> GetJsonAsync(string url, CancellationToken ct)
    {
        try
        {
            using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return null;

            var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(text))
                return null;

            using var document = JsonDocument.Parse(text);
            return document.RootElement.Clone();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.Warn($"Music: LRCLIB 请求失败（{url}）—— {ex.Message}");
            return null;
        }
    }

    /// <summary>把 LRCLIB 的响应（对象或数组）映射成「候选 + 时间轴」；只认 syncedLyrics。</summary>
    private static LyricsHit? ToHit(JsonElement root, bool expectArray, LyricsQuery query)
    {
        if (expectArray)
        {
            if (root.ValueKind != JsonValueKind.Array)
                return null;

            // 单级判定：候选不可信就不采用（不做"退而求其次"——那会把别人的歌词显示给你）
            foreach (var item in root.EnumerateArray())
            {
                // 认领规则与网易云源共用一份（LyricsMatcher），避免两处逻辑分叉
                var candidate = ToCandidate(item);
                if (candidate is null || !LyricsMatcher.IsTrustworthy(candidate, query))
                    continue;

                var hit = ToHit(item, expectArray: false, query);
                if (hit is not null)
                    return hit;
            }

            return null;
        }

        if (root.ValueKind != JsonValueKind.Object)
            return null;

        if (root.TryGetProperty("instrumental", out var instrumental) &&
            instrumental.ValueKind == JsonValueKind.True)
        {
            return null;
        }

        if (!root.TryGetProperty("syncedLyrics", out var synced) ||
            synced.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var timeline = LrcParser.Parse(synced.GetString());
        if (timeline.IsEmpty)
            return null;

        // 候选信息用于跨源打分；万一响应里缺字段，退化成"按查询自身"（分不出高下时也就是它）
        var self = ToCandidate(root)
            ?? new LyricsCandidate(string.Empty, query.Title, query.Artist, query.Album, query.Duration);

        return new LyricsHit(self, timeline);
    }

    /// <summary>把 LRCLIB 的搜索结果映射成共用候选（时长 LRCLIB 给的是秒）。</summary>
    private static LyricsCandidate? ToCandidate(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object)
            return null;

        var title = ReadString(item, "trackName");
        if (title is null)
            return null;

        var duration = item.TryGetProperty("duration", out var value) && value.ValueKind == JsonValueKind.Number
            ? TimeSpan.FromSeconds(value.GetDouble())
            : TimeSpan.Zero;

        return new LyricsCandidate(
            ReadString(item, "id") ?? string.Empty,
            title,
            ReadString(item, "artistName") ?? string.Empty,
            ReadString(item, "albumName") ?? string.Empty,
            duration);
    }

    private static string? ReadString(JsonElement item, string property) =>
        item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    // ---------------- 磁盘缓存 ----------------
    // 缓存「一行候选头 + 原始 LRC 文本」：
    //   * 存原始文本（不是解析结果）→ 解析规则将来改进，旧缓存也能受益；
    //   * 存候选头 → 缓存命中时仍能参与跨源打分（只存文本的话，缓存里的命中就没法比较）。
    // **旧版本写的「没有候选头」的缓存一律视为过期**：不采用、重新联网取，并把该文件删掉 ——
    // 否则旧的错误匹配会被缓存永久钉住（这正是"歌词一直不对"的成因之一），白白占着目录。
    //
    // 目录与用户的本地歌词共用（LocalLrcProvider 也读这里），所以一切删除都走 LyricsCache ——
    // 它只碰 `^[0-9A-F]{16}\.lrc$` 的缓存文件，手写的 .lrc 不受影响。

    private const string HeaderPrefix = "#candidate ";
    private const char HeaderSeparator = '\u001f';

    private string CachePath(LyricsQuery query)
    {
        var bytes = Encoding.UTF8.GetBytes(query.Key);
        var hash = Convert.ToHexString(SHA256.HashData(bytes))[..16];
        return Path.Combine(_cacheDirectory, hash + ".lrc");
    }

    private LyricsHit? ReadCache(LyricsQuery query)
    {
        if (string.IsNullOrEmpty(_cacheDirectory))
            return null;

        try
        {
            var path = CachePath(query);
            if (!File.Exists(path))
                return null;

            var text = File.ReadAllText(path);
            var (header, body) = SplitHeader(text);
            if (header is null)
            {
                DeleteStale(path);           // 旧格式（无候选头）→ 过期：删掉，别永远占着目录
                return null;
            }

            var timeline = LrcParser.Parse(body);
            if (timeline.IsEmpty)
                return null;

            var candidate = ParseHeader(header)
                ?? new LyricsCandidate(string.Empty, query.Title, query.Artist, query.Album, query.Duration);

            Touch(path);                     // 命中即"最近使用"：淘汰顺序反映最后使用，而不是首次写入

            return new LyricsHit(candidate, timeline);
        }
        catch (Exception ex)
        {
            Logger.Warn($"Music: 歌词缓存读取失败 —— {ex.Message}");
            return null;
        }
    }

    /// <summary>命中缓存后回写访问时间（best-effort：失败只影响将来的淘汰顺序，不影响取词）。</summary>
    private static void Touch(string path)
    {
        try
        {
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            Logger.Warn($"Music: 歌词缓存访问时间回写失败（{path}）—— {ex.Message}");
        }
    }

    /// <summary>删除无候选头的过期缓存（<see cref="LyricsCache.IsCacheFile"/> 兜底，用户歌词不可能被命中）。</summary>
    private static void DeleteStale(string path)
    {
        try
        {
            if (LyricsCache.IsCacheFile(Path.GetFileName(path)))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            Logger.Warn($"Music: 过期歌词缓存删除失败（{path}）—— {ex.Message}");
        }
    }

    /// <summary>按上限回收一次磁盘缓存（LRU：最久未用先删）；真的删了才写日志。</summary>
    private void PruneCache()
    {
        if (string.IsNullOrEmpty(_cacheDirectory))
            return;

        var (deleted, freed) = LyricsCache.Prune(_cacheDirectory, _cacheLimitBytes);
        if (deleted > 0)
        {
            Logger.Info($"Music: 歌词缓存超出上限（{LyricsCache.FormatSize(_cacheLimitBytes)}）→ " +
                        $"回收 {deleted} 个 / 释放 {LyricsCache.FormatSize(freed)}");
        }
    }

    private void WriteCache(LyricsQuery query, LyricsHit hit)
    {
        if (string.IsNullOrEmpty(_cacheDirectory))
            return;

        try
        {
            Directory.CreateDirectory(_cacheDirectory);

            var lines = new StringBuilder();
            lines.Append(HeaderPrefix).Append(FormatHeader(hit.Candidate)).Append('\n');
            foreach (var line in hit.Timeline.Lines)
                lines.Append('[').Append(line.Time.ToString(@"mm\:ss\.fff")).Append(']').Append(line.Text).Append('\n');

            File.WriteAllText(CachePath(query), lines.ToString());
            PruneCache();                       // 写成功才回收：超限先删最久未用的
        }
        catch (Exception ex)
        {
            Logger.Warn($"Music: 歌词缓存写入失败 —— {ex.Message}");
        }
    }

    private static string FormatHeader(LyricsCandidate candidate) => string.Join(
        HeaderSeparator,
        candidate.Id,
        candidate.Title,
        candidate.Artist,
        candidate.Album,
        ((int)Math.Round(candidate.Duration.TotalSeconds)).ToString());

    private static LyricsCandidate? ParseHeader(string header)
    {
        var parts = header.Split(HeaderSeparator);
        if (parts.Length < 5)
            return null;

        return new LyricsCandidate(
            parts[0],
            parts[1],
            parts[2],
            parts[3],
            int.TryParse(parts[4], out int seconds) ? TimeSpan.FromSeconds(seconds) : TimeSpan.Zero);
    }

    private static (string? Header, string Body) SplitHeader(string text)
    {
        if (!text.StartsWith(HeaderPrefix, StringComparison.Ordinal))
            return (null, text);

        int end = text.IndexOf('\n');
        return end < 0
            ? (null, text)
            : (text[HeaderPrefix.Length..end], text[(end + 1)..]);
    }
}

/// <summary>
/// 本地 .lrc：在插件数据目录的 <c>lyrics\</c> 下按常见命名找文件。
/// <para>
/// 现版 SMTC **不提供音频文件路径**，所以只能靠命名约定匹配。依次尝试：
/// <c>艺术家 - 曲名.lrc</c> → <c>曲名.lrc</c> → <c>曲名 - 艺术家.lrc</c>（大小写不敏感）。
/// </para>
/// </summary>
public sealed class LocalLrcProvider : ICandidateLyricsProvider
{
    private readonly string _directory;

    /// <param name="directory">歌词目录（<c>&lt;插件数据目录&gt;\lyrics</c>）。</param>
    public LocalLrcProvider(string directory) => _directory = directory ?? string.Empty;

    /// <inheritdoc />
    public string Name => "本地";

    /// <inheritdoc />
    public async Task<LrcTimeline?> FetchAsync(LyricsQuery query, CancellationToken ct) =>
        (await LookupAsync(query, ct).ConfigureAwait(false))?.Timeline;

    /// <inheritdoc />
    public Task<LyricsHit?> LookupAsync(LyricsQuery query, CancellationToken ct)
    {
        if (!query.IsUsable || string.IsNullOrEmpty(_directory))
            return Task.FromResult<LyricsHit?>(null);

        foreach (var name in CandidateNames(query))
        {
            var path = Path.Combine(_directory, name);
            if (!File.Exists(path))
                continue;

            try
            {
                var timeline = LrcParser.Parse(File.ReadAllText(path));
                if (!timeline.IsEmpty)
                {
                    // 本地命中的"匹配度"来自文件名约定（艺术家 - 曲名 / 曲名），已经等于"就是这首歌"；
                    // 候选按查询自身构造，好让它能站到并行择优里跟在线源一起比。
                    return Task.FromResult<LyricsHit?>(new LyricsHit(
                        new LyricsCandidate("local:" + name, query.Title, query.Artist, query.Album, query.Duration),
                        timeline));
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"Music: 本地歌词读取失败（{path}）—— {ex.Message}");
            }
        }

        return Task.FromResult<LyricsHit?>(null);
    }

    private static IEnumerable<string> CandidateNames(LyricsQuery query)
    {
        var artist = Sanitize(query.Artist);
        var title = Sanitize(query.Title);

        if (!string.IsNullOrEmpty(artist))
            yield return $"{artist} - {title}.lrc";

        yield return $"{title}.lrc";

        if (!string.IsNullOrEmpty(artist))
            yield return $"{title} - {artist}.lrc";
    }

    private static string Sanitize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var invalid = Path.GetInvalidFileNameChars();
        var parts = value.Split(invalid, StringSplitOptions.RemoveEmptyEntries);
        return string.Join("_", parts).Trim();
    }
}
