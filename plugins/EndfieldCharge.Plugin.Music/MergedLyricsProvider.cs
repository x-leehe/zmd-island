using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EndfieldCharge.Services;

namespace EndfieldCharge.Host.Plugins.Music;

/// <summary>
/// 多源并行择优（设置里的「并行择优」）：**同时**问所有来源，汇总成一份候选列表，
/// 再挑最像这首歌的那一条。
/// <para>
/// 为什么不是「顺序回退，取第一个命中的」：第一个源"有结果"不等于"结果是对的" ——
/// 实测 LRCLIB 会把同名 / 近名的另一首当命中返回，顺序回退就再也没有机会去问更准的源。
/// 并行汇总让各源结果同台比较，谁更像谁上。
/// </para>
/// <para>
/// 同分（几乎只可能是同一份歌词）时取构造顺序里靠前的。要"某个源优先"请用串行回退
/// （<see cref="FallbackLyricsProvider"/>）—— 那是「偏好 X」，语义是顺序而非比较。
/// </para>
/// </summary>
public sealed class MergedLyricsProvider : ILyricsProvider
{
    private readonly IReadOnlyList<ICandidateLyricsProvider> _sources;

    /// <param name="sources">参与汇总的来源，**按偏好从高到低**。</param>
    public MergedLyricsProvider(params ICandidateLyricsProvider[] sources)
        => _sources = sources ?? Array.Empty<ICandidateLyricsProvider>();

    /// <summary>参与汇总的来源（日志 / 排查用）。</summary>
    public IReadOnlyList<ICandidateLyricsProvider> Sources => _sources;

    /// <inheritdoc />
    public async Task<LrcTimeline?> FetchAsync(LyricsQuery query, CancellationToken ct)
    {
        if (!query.IsUsable || _sources.Count == 0)
            return null;

        // 并行探测：一个源慢（或超时）不该让另一个源的结果跟着等
        var hits = await Task.WhenAll(_sources.Select(source => LookupAsync(source, query, ct)))
            .ConfigureAwait(false);

        var best = LyricsMatcher.PickBest(hits, query);
        if (best is null)
        {
            Logger.Info($"Music: 多源汇总无可信歌词（{Describe(query)} · {_sources.Count} 个来源）");
            return null;
        }

        Logger.Info($"Music: 多源择优 → {Describe(best.Candidate)}（{_sources.Count} 个来源）");
        return best.Timeline;
    }

    /// <summary>单个来源失败不该拖垮整条链：记一行，当作「这一源没结果」。</summary>
    private static async Task<LyricsHit?> LookupAsync(
        ICandidateLyricsProvider source, LyricsQuery query, CancellationToken ct)
    {
        try
        {
            return await source.LookupAsync(query, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 换曲打断：正常路径
            return null;
        }
        catch (Exception ex)
        {
            Logger.Warn($"Music: 歌词源 {source.Name} 失败 —— {ex.GetType().Name} {ex.Message}");
            return null;
        }
    }

    private static string Describe(LyricsQuery query) =>
        string.IsNullOrWhiteSpace(query.Artist) ? query.Title : $"{query.Artist} - {query.Title}";

    private static string Describe(LyricsCandidate candidate) =>
        string.IsNullOrWhiteSpace(candidate.Artist)
            ? candidate.Title
            : $"{candidate.Artist} - {candidate.Title}";
}
