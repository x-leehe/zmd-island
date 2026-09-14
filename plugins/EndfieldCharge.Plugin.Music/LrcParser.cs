using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace EndfieldCharge.Host.Plugins.Music;

/// <summary>一行歌词（时间戳已按行内 <c>[offset:]</c> 修正）。</summary>
public sealed record LrcLine(TimeSpan Time, string Text);

/// <summary>
/// 歌词时间轴：把解析结果包成"随播放位置取当前行"的查询。
/// <para>
/// 语义：返回<strong>此位置之前最后一个有文字的行</strong> —— 行与行之间保持上一句，
/// 而不是回退成空（否则每当句间停顿就会闪回歌名占位）。
/// </para>
/// </summary>
public sealed class LrcTimeline
{
    /// <summary>空时间轴（没有歌词 / 解析不出内容）。</summary>
    public static readonly LrcTimeline Empty = new(Array.Empty<LrcLine>());

    private readonly LrcLine[] _lines;

    /// <param name="lines">已按时间升序、且只含有文字的行。</param>
    public LrcTimeline(IReadOnlyList<LrcLine> lines)
    {
        var kept = new List<LrcLine>(lines.Count);
        foreach (var line in lines)
        {
            if (line is null || string.IsNullOrWhiteSpace(line.Text))
                continue;
            kept.Add(line);
        }

        kept.Sort(static (a, b) => a.Time.CompareTo(b.Time));
        _lines = kept.ToArray();
    }

    /// <summary>没有可显示的歌词。</summary>
    public bool IsEmpty => _lines.Length == 0;

    /// <summary>解析出的歌词行（升序，已剔除空行）。</summary>
    public IReadOnlyList<LrcLine> Lines => _lines;

    /// <summary>最后一行没有"下一行"可参考时，按这个时长估算它的跨度。</summary>
    private const double TailSeconds = 5d;

    /// <summary>
    /// 取该播放位置应显示的歌词；早于第一句（或没有歌词）时返回空串。
    /// 用二分查找 —— 会被 250ms 的进度 tick 频繁调用。
    /// </summary>
    public string CurrentAt(TimeSpan position)
    {
        int index = IndexAt(position);
        return index < 0 ? string.Empty : _lines[index].Text;
    }

    /// <summary>
    /// 该位置在**当前这一行内**的进度（0..1）：行首 0、下一行的时刻为 1。
    /// <para>
    /// 长句的可视窗口靠它"跟到歌手唱到的地方"—— 进度直接来自 LRC 的时间轨道，
    /// 而不是自己定速循环。最后一行没有下一行可参考，按 <see cref="TailSeconds"/> 估算跨度。
    /// </para>
    /// </summary>
    public double ProgressAt(TimeSpan position)
    {
        int index = IndexAt(position);
        if (index < 0)
            return 0d;

        var start = _lines[index].Time;
        var end = index + 1 < _lines.Length
            ? _lines[index + 1].Time
            : start + TimeSpan.FromSeconds(TailSeconds);

        double span = (end - start).TotalSeconds;
        if (span <= 0.05d)
            return 1d;                                    // 极短的一句：当作已经唱完

        return Math.Clamp((position - start).TotalSeconds / span, 0d, 1d);
    }

    /// <summary>二分查找当前位置所在的行（<c>-1</c> = 早于第一句 / 没有歌词）。</summary>
    private int IndexAt(TimeSpan position)
    {
        if (_lines.Length == 0)
            return -1;

        int lo = 0, hi = _lines.Length - 1, hit = -1;
        while (lo <= hi)
        {
            int mid = lo + ((hi - lo) >> 1);
            if (_lines[mid].Time <= position)
            {
                hit = mid;
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }

        return hit;
    }
}

/// <summary>
/// LRC 解析。支持：
/// <list type="bullet">
///   <item><c>[mm:ss]</c>、<c>[mm:ss.x]</c>、<c>[mm:ss.xx]</c>、<c>[mm:ss.xxx]</c>（小数位按位权换算）</item>
///   <item>一行挂多个时间戳（同一句在不同时刻重复出现）</item>
///   <item><c>[offset:±毫秒]</c>（全局平移，按标准取原值直接相加）</item>
///   <item>忽略 <c>[ti:]</c> <c>[ar:]</c> <c>[al:]</c> <c>[by:]</c> 等元信息标签与无法识别的行</item>
/// </list>
/// 不含任何 IO / 网络 / UI 依赖，便于单元测试。
/// </summary>
public static class LrcParser
{
    private static readonly Regex TimeTag = new(
        @"\[(\d{1,3}):(\d{1,2})(?:[.:](\d{1,3}))?\]", RegexOptions.Compiled);

    private static readonly Regex OffsetTag = new(
        @"\[offset:\s*([+-]?\d+)\s*\]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>增强型 LRC 的逐字时间戳内核（形如 <c>mm:ss.ms</c>，不带括号）。</summary>
    private static readonly Regex WordTimestamp = new(
        @"^\d{1,3}:\d{1,2}(?:[.:]\d{1,3})?$", RegexOptions.Compiled);

    /// <summary>解析一段 LRC 文本；任何解析不出的内容都被忽略（不会抛异常）。</summary>
    public static LrcTimeline Parse(string? lrc)
    {
        if (string.IsNullOrWhiteSpace(lrc))
            return LrcTimeline.Empty;

        TimeSpan offset = TimeSpan.Zero;
        var raw = new List<LrcLine>();

        foreach (var sourceLine in lrc.Split('\n'))
        {
            var line = sourceLine.Trim();
            if (line.Length == 0)
                continue;

            var offsetMatch = OffsetTag.Match(line);
            if (offsetMatch.Success &&
                int.TryParse(offsetMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int offsetMs))
            {
                offset = TimeSpan.FromMilliseconds(offsetMs);
                continue;
            }

            var matches = TimeTag.Matches(line);
            if (matches.Count == 0)
                continue;   // 元信息标签或纯文本行

            var last = matches[^1];
            var text = StripWordTimestamps(line[(last.Index + last.Length)..]).Trim();

            foreach (Match match in matches)
            {
                var time = ToTimeSpan(match);
                if (time is not null)
                    raw.Add(new LrcLine(time.Value, text));
            }
        }

        if (raw.Count == 0)
            return LrcTimeline.Empty;

        if (offset != TimeSpan.Zero)
        {
            for (int i = 0; i < raw.Count; i++)
                raw[i] = raw[i] with { Time = raw[i].Time + offset };
        }

        return new LrcTimeline(raw);
    }

    /// <summary>
    /// 剥掉增强型 LRC 的逐字时间戳（<c>[00:12.34]&lt;00:12.34&gt;词&lt;00:12.80&gt;语</c> 里的尖括号部分）。
    /// 只剥**内容长得像时间**的尖括号 —— 否则会把正文里的 <c>&lt;3</c>、<c>&lt;not time&gt;</c> 一起吃掉。
    /// 岛上一次只显示一整句，逐字时间轴对本项目没有用处。
    /// </summary>
    private static string StripWordTimestamps(string text)
    {
        if (text.IndexOf('<') < 0 || text.IndexOf('>') < 0)
            return text;

        var result = new StringBuilder(text.Length);
        int i = 0;
        while (i < text.Length)
        {
            if (text[i] == '<')
            {
                int close = text.IndexOf('>', i + 1);
                if (close > i && WordTimestamp.IsMatch(text[(i + 1)..close].Trim()))
                {
                    i = close + 1;
                    continue;
                }
            }

            result.Append(text[i]);
            i++;
        }

        return result.ToString();
    }

    private static TimeSpan? ToTimeSpan(Match match)
    {
        if (!int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int minutes) ||
            !int.TryParse(match.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int seconds))
        {
            return null;
        }

        if (seconds > 59)
            seconds = 59;

        int milliseconds = 0;
        if (match.Groups[3].Success)
        {
            var frac = match.Groups[3].Value;
            int value = int.Parse(frac, NumberStyles.Integer, CultureInfo.InvariantCulture);
            milliseconds = frac.Length switch
            {
                1 => value * 100,
                2 => value * 10,
                _ => int.Parse(frac[..3], NumberStyles.Integer, CultureInfo.InvariantCulture),
            };
        }

        return new TimeSpan(0, 0, minutes, seconds, milliseconds);
    }
}
