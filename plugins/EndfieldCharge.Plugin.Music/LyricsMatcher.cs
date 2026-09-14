using System;
using System.Collections.Generic;
using System.Text;

namespace EndfieldCharge.Host.Plugins.Music;

/// <summary>搜索命中的候选曲目：各歌词源把自己的响应映射成这个形状，认领规则因此只需一份。</summary>
public sealed record LyricsCandidate(string Id, string Title, string Artist, string Album, TimeSpan Duration);

/// <summary>
/// 候选认领规则（各歌词源共用）。总原则：**宁可不出歌词，也不要出错歌词** ——
/// 实测踩过"同名不同歌手、时长还恰好接近"的坑。
/// <para>
/// 曲名用**相似度**而不是严格相等（借 waylyrics 的 bigram Dice 系数）：各源的曲名习惯不同
/// —— 网易云会把「灰すら遺らなくても。」的句号一起放进曲名、流媒体会加 "(Sped up)" 后缀 ——
/// 严格相等会把这些**同一首歌**判成不匹配；相似度 + 阈值既宽容又不会认错歌。
/// 也因此**不需要手工去剥标点**：标题保持作者原样。
/// </para>
/// </summary>
public static class LyricsMatcher
{
    private static readonly char[] ArtistsSeparators = { ',', '，', '&', '/', '、', ';', '；', '+', '|' };

    /// <summary>时长容差（秒）：LRCLIB 与网易云给的都是整秒/毫秒，留 3 秒足够。</summary>
    public const double DurationToleranceSeconds = 3d;

    /// <summary>
    /// 曲名相似度阈值：低于它就认为"不是同一首"（宁缺勿错）。
    /// 0.80 ≈ 只差尾部一两个字符（"…ても。" vs "…ても" 约 0.96）、或带一个版本后缀仍可命中。
    /// </summary>
    public const double TitleSimilarityThreshold = 0.80d;

    /// <summary>
    /// 候选是否可信：**曲名足够像**，且
    /// <list type="bullet">
    ///   <item>已知艺术家时：艺术家词元要有交集（<c>TIC,Paperman</c> 拆成 TIC / Paperman）；</item>
    ///   <item>艺术家未知时：退而要求时长吻合。</item>
    /// </list>
    /// </summary>
    public static bool IsTrustworthy(LyricsCandidate candidate, LyricsQuery query)
    {
        if (candidate is null || TitleSimilarity(candidate.Title, query.Title) < TitleSimilarityThreshold)
            return false;

        return string.IsNullOrWhiteSpace(query.Artist)
            ? DurationMatches(candidate.Duration, query.Duration)
            : ArtistOverlaps(candidate.Artist, query.Artist);
    }

    /// <summary>在候选里挑第一条可信的；没有就返回 <c>null</c>（宁缺勿错）。</summary>
    public static LyricsCandidate? Pick(IReadOnlyList<LyricsCandidate> candidates, LyricsQuery query)
    {
        foreach (var candidate in candidates)
        {
            if (IsTrustworthy(candidate, query))
                return candidate;
        }

        return null;
    }

    /// <summary>
    /// 跨来源择优：在**可信**且**不是纯音乐占位**的结果里取分最高的那个。
    /// <para>
    /// 同分（几乎只可能是同一份歌词）时保留列表里靠前的 —— 那是来源的排列顺序，不是"偏好"：
    /// 「偏好 X」由串行回退实现（<see cref="FallbackLyricsProvider"/>），语义是顺序，不参与比大小。
    /// </para>
    /// </summary>
    public static LyricsHit? PickBest(IReadOnlyList<LyricsHit?> hits, LyricsQuery query)
    {
        LyricsHit? best = null;
        double bestScore = double.NegativeInfinity;

        foreach (var hit in hits)
        {
            if (hit is null || !IsTrustworthy(hit.Candidate, query) || IsInstrumentalFiller(hit.Timeline))
                continue;

            double score = Score(hit.Candidate, query);
            if (score > bestScore)
            {
                bestScore = score;
                best = hit;
            }
        }

        return best;
    }

    /// <summary>
    /// 候选得分（0..1，多源汇总择优用）。前提是候选已经**可信**（见 <see cref="IsTrustworthy"/>）。
    /// <para>
    /// 权重：曲名 0.65 / 艺术家 0.25 / 时长 0.10。曲名是唯一能区分「是不是这首」的字段，给大头；
    /// 艺术家用来压过「同名不同歌手」；时长只做微调（同曲不同版本时长会差）。
    /// 未知字段给 0.5 —— 不奖励也不惩罚，免得"信息少的那一方"被系统性压分。
    /// </para>
    /// </summary>
    public static double Score(LyricsCandidate candidate, LyricsQuery query)
    {
        double title = TitleSimilarity(candidate.Title, query.Title);

        double artist = string.IsNullOrWhiteSpace(query.Artist)
            ? 0.5d
            : ArtistOverlaps(candidate.Artist, query.Artist) ? 1d : 0d;

        double duration;
        if (candidate.Duration <= TimeSpan.Zero || query.Duration <= TimeSpan.Zero)
        {
            duration = 0.5d;
        }
        else
        {
            var delta = Math.Abs((candidate.Duration - query.Duration).TotalSeconds);
            duration = Math.Clamp(1d - (delta / DurationToleranceSeconds), 0d, 1d);
        }

        return (0.65d * title) + (0.25d * artist) + (0.10d * duration);
    }

    /// <summary>占位歌词最多几行：真歌词不可能只有 1~3 行。</summary>
    private const int MaxFillerLines = 3;

    /// <summary>纯音乐占位歌词的标记词（去空白与标点后匹配）。</summary>
    private static readonly string[] FillerMarkers =
    {
        "纯音乐", "请欣赏", "请期待", "无歌词", "暂无歌词", "没有填词", "无填词", "此歌曲为", "instrumental",
    };

    /// <summary>
    /// 「纯音乐，请欣赏」这类**占位**歌词：它们在时间轴上"有内容"，但不含任何歌词信息 ——
    /// 各源对纯音乐曲目会给一行提示而不是留空，不过滤就会被当成歌词显示。
    /// <para>
    /// 判定刻意保守：**行数 ≤ 3 且**文本命中标记词才算（真歌词里出现"纯音乐"三个字不该被误杀）。
    /// </para>
    /// </summary>
    public static bool IsInstrumentalFiller(LrcTimeline timeline)
    {
        if (timeline.IsEmpty || timeline.Lines.Count > MaxFillerLines)
            return false;

        var text = new StringBuilder();
        foreach (var line in timeline.Lines)
            text.Append(line.Text);

        var compact = new StringBuilder(text.Length);
        foreach (var ch in text.ToString().ToLowerInvariant())
        {
            if (!char.IsWhiteSpace(ch) && !char.IsPunctuation(ch) && !char.IsSymbol(ch))
                compact.Append(ch);
        }

        var value = compact.ToString();
        foreach (var marker in FillerMarkers)
        {
            if (value.Contains(marker, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <summary>
    /// 曲名相似度：字符二元组集合的 Dice 系数，并取它与**单边包含度**的较大者。
    /// <list type="bullet">
    ///   <item>先只在**比对用**的副本上去掉版本后缀（<c>歌名</c> ⊂ <c>歌名 （Sped up）</c>），标点保持原样；</item>
    ///   <item>对称 Dice 对"差一两个字符"宽容（<c>…ても。</c> vs <c>…ても</c> ≈ 0.96），
    ///         但会被更长的后缀压垮（短名 + 长后缀只有 0.17），所以再取一次单边包含度补上。</item>
    /// </list>
    /// </summary>
    public static double TitleSimilarity(string left, string right)
    {
        var a = Bigrams(NormalizeTitle(Normalize(left)));
        var b = Bigrams(NormalizeTitle(Normalize(right)));

        if (a.Count == 0 || b.Count == 0)
            return string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase) ? 1d : 0d;

        int shared = 0;
        foreach (var pair in a)
        {
            if (b.Contains(pair))
                shared++;
        }

        double dice = 2d * shared / (a.Count + b.Count);
        double containment = (double)shared / Math.Min(a.Count, b.Count);
        return Math.Max(dice, containment);
    }

    /// <summary>艺术家词元是否有交集（按常见分隔符拆开，忽略大小写，忽略 1 字符碎片）。</summary>
    public static bool ArtistOverlaps(string? candidate, string wanted)
    {
        if (string.IsNullOrWhiteSpace(candidate))
            return false;

        var others = ArtistTokens(candidate);
        foreach (var token in ArtistTokens(wanted))
        {
            foreach (var other in others)
            {
                if (string.Equals(token, other, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    /// <summary>时长是否吻合（任一方未知就不筛）。</summary>
    public static bool DurationMatches(TimeSpan candidate, TimeSpan wanted) =>
        candidate <= TimeSpan.Zero ||
        wanted <= TimeSpan.Zero ||
        Math.Abs((candidate - wanted).TotalSeconds) <= DurationToleranceSeconds;

    /// <summary>
    /// **生成搜索关键词**时用的曲名：去掉尾部括号后缀（<c>(Sped up)</c> / <c>（Sped up）</c> / <c>[Live]</c>…），
    /// 这样"只按曲名搜"那一次能命中原版。
    /// <para>只用于搜索，不用于比对 —— 比对走 <see cref="TitleSimilarity"/>。</para>
    /// </summary>
    public static string NormalizeTitle(string title)
    {
        var value = (title ?? string.Empty).Trim();
        while (value.Length >= 2)
        {
            var open = value[^1] switch
            {
                ')' => '(',
                ']' => '[',
                '）' => '（',
                '】' => '【',
                _ => '\0',
            };
            if (open == '\0')
                break;

            int start = value.LastIndexOf(open);
            if (start <= 0)
                break;

            value = value[..start].TrimEnd();
        }

        return value.Length == 0 ? (title ?? string.Empty).Trim() : value;
    }

    private static string Normalize(string text) => (text ?? string.Empty).Trim().ToLowerInvariant();

    private static HashSet<string> Bigrams(string text)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (text.Length == 0)
            return set;

        if (text.Length == 1)
        {
            set.Add(text);
            return set;
        }

        for (int i = 0; i + 1 < text.Length; i++)
            set.Add(text.Substring(i, 2));

        return set;
    }

    private static List<string> ArtistTokens(string value)
    {
        var tokens = new List<string>();
        foreach (var part in value.Split(ArtistsSeparators, StringSplitOptions.RemoveEmptyEntries))
        {
            var token = part.Trim();
            if (token.Length >= 2)
                tokens.Add(token);
        }

        return tokens;
    }
}
