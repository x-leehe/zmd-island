using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EndfieldCharge.Services;

namespace EndfieldCharge.Host.Plugins.Music;

/// <summary>
/// 网易云音乐歌词源：补 LRCLIB 覆盖不到的中日韩曲目（实测 LRCLIB 对日文歌一条都没有，而这边一次命中且带翻译）。
/// <para>
/// 用的是公开但**非官方**的接口（搜索 + 歌词），与各类桌面歌词工具同一路子：需带 <c>Referer</c>，
/// 可能受地区 / 风控影响 —— 因此任何失败都静默降级（返回 <c>null</c>），不影响其它源。
/// </para>
/// </summary>
public sealed class NeteaseProvider : ICandidateLyricsProvider
{
    private const string SearchEndpoint = "https://music.163.com/api/search/get/web";
    private const string LyricEndpoint = "https://music.163.com/api/song/lyric";
    private const string Referer = "https://music.163.com";
    private const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36";

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(6);

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private static readonly Lazy<HttpClient> SharedClient = new(() =>
    {
        var client = new HttpClient { Timeout = RequestTimeout };
        client.DefaultRequestHeaders.Referrer = new Uri(Referer);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        return client;
    });

    private readonly HttpClient _http;
    private readonly Dictionary<string, LyricsHit?> _memory = new(StringComparer.Ordinal);

    /// <param name="httpClient">测试可注入；为空时用共享实例。</param>
    public NeteaseProvider(HttpClient? httpClient = null) => _http = httpClient ?? SharedClient.Value;

    /// <inheritdoc />
    public string Name => "网易云";

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

        // 两级关键词：先「艺术家 + 曲名」（准），不行再只按曲名（宽）；候选仍要过认领规则
        var hit = await TryKeywordAsync($"{query.Artist} {query.Title}".Trim(), query, ct)
            .ConfigureAwait(false);

        if (hit is null && !string.IsNullOrWhiteSpace(query.Artist))
            hit = await TryKeywordAsync(query.Title, query, ct).ConfigureAwait(false);

        if (hit is not null)
            Logger.Info($"Music: 歌词命中（网易云 · {Describe(query)} · {hit.Timeline.Lines.Count} 句）");

        _memory[query.Key] = hit;
        return hit;
    }

    private async Task<LyricsHit?> TryKeywordAsync(string keyword, LyricsQuery query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return null;

        var candidates = await SearchAsync(keyword, ct).ConfigureAwait(false);
        if (candidates.Count == 0)
            return null;

        var picked = LyricsMatcher.Pick(candidates, query);
        if (picked is null)
            return null;

        var timeline = await FetchLyricAsync(picked.Id, ct).ConfigureAwait(false);
        return timeline is null ? null : new LyricsHit(picked, timeline);
    }

    /// <summary>搜索：<c>/api/search/get/web</c> → 映射成候选（时长是毫秒）。</summary>
    private async Task<IReadOnlyList<LyricsCandidate>> SearchAsync(string keyword, CancellationToken ct)
    {
        var url = $"{SearchEndpoint}?type=1&limit=8&s={Uri.EscapeDataString(keyword)}";
        var root = await GetJsonAsync(url, ct).ConfigureAwait(false);
        var candidates = new List<LyricsCandidate>();

        if (root is null || !root.Value.TryGetProperty("result", out var result) ||
            !result.TryGetProperty("songs", out var songs) || songs.ValueKind != JsonValueKind.Array)
        {
            return candidates;
        }

        foreach (var song in songs.EnumerateArray())
        {
            var id = song.TryGetProperty("id", out var idValue) && idValue.ValueKind == JsonValueKind.Number
                ? idValue.GetInt64().ToString()
                : null;
            if (id is null)
                continue;

            var title = ReadString(song, "name") ?? string.Empty;

            var artists = new List<string>();
            if (song.TryGetProperty("artists", out var artistArray) && artistArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var artist in artistArray.EnumerateArray())
                {
                    var name = ReadString(artist, "name");
                    if (!string.IsNullOrWhiteSpace(name))
                        artists.Add(name);
                }
            }

            var album = string.Empty;
            if (song.TryGetProperty("album", out var albumObject) && albumObject.ValueKind == JsonValueKind.Object)
                album = ReadString(albumObject, "name") ?? string.Empty;

            // 搜索结果里的 duration 是毫秒
            var duration = song.TryGetProperty("duration", out var durationValue) && durationValue.ValueKind == JsonValueKind.Number
                ? TimeSpan.FromMilliseconds(durationValue.GetDouble())
                : TimeSpan.Zero;

            candidates.Add(new LyricsCandidate(id, title, string.Join(",", artists), album, duration));
        }

        return candidates;
    }

    /// <summary>取词：<c>/api/song/lyric</c> → <c>lrc.lyric</c>（翻译在 <c>tlyric</c>，本项目一行位暂不用）。</summary>
    private async Task<LrcTimeline?> FetchLyricAsync(string songId, CancellationToken ct)
    {
        var url = $"{LyricEndpoint}?id={Uri.EscapeDataString(songId)}&lv=1&kv=1&tv=-1";
        var root = await GetJsonAsync(url, ct).ConfigureAwait(false);
        if (root is null || !root.Value.TryGetProperty("lrc", out var lrc) || lrc.ValueKind != JsonValueKind.Object)
            return null;

        var text = ReadString(lrc, "lyric");
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var timeline = LrcParser.Parse(text);
        return timeline.IsEmpty ? null : timeline;
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
            Logger.Warn($"Music: 网易云接口失败（{url}）—— {ex.GetType().Name} {ex.Message}");
            return null;
        }
    }

    private static string? ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string Describe(LyricsQuery query) =>
        string.IsNullOrWhiteSpace(query.Artist) ? query.Title : $"{query.Artist} - {query.Title}";
}
