using System;

namespace EndfieldCharge.Host.Plugins.Music;

/// <summary>
/// 长句歌词的**分页**推进（纯逻辑）：判断"这一屏快唱完了没有、要不要把后一段 unfold 出来"。
/// <para>
/// 两条不变量，都是被实际观感逼出来的：
/// <list type="number">
///   <item>只有唱到逼近当前屏右缘（留 <c>margin</c> 余量）才动 —— 文本绝大多数时间应当**不动**；</item>
///   <item>只允许**向前** —— 播放位置的细微抖动不许把已经展开的内容收回去。</item>
/// </list>
/// </para>
/// </summary>
public static class LyricPager
{
    /// <summary>翻一屏时前进的比例；剩下的部分当作重叠，读起来不会断。</summary>
    public const double AdvanceRatio = 0.75d;

    /// <summary>小于这个步长就当没动，避免在同一位置反复起补间。</summary>
    public const double MinStep = 0.005d;

    /// <summary>
    /// 是否该翻页。<paramref name="pageStart"/> / <paramref name="visible"/> 都是**文本比例**
    /// （当前窗口起点 / 一屏能显示整句的几成），<paramref name="progress"/> 为行内唱到的位置。
    /// </summary>
    /// <param name="next">该翻页时写入新的窗口起点；否则等于原值。</param>
    /// <returns>该翻页返回 <c>true</c>。</returns>
    public static bool TryAdvance(double pageStart, double visible, double progress, double margin, out double next)
    {
        next = pageStart;

        if (visible <= 0d || visible >= 1d)
            return false;                                  // 一屏放得下 / 参数不合理

        double maxStart = Math.Max(0d, 1d - visible);
        if (pageStart >= maxStart)
            return false;                                  // 已经滑到句尾

        double sung = Math.Clamp(progress, 0d, 1d);
        if (sung < pageStart + visible - margin)
            return false;                                  // 还早：展示的这些还没快唱完

        double candidate = Math.Min(pageStart + (visible * AdvanceRatio), maxStart);
        if (candidate <= pageStart + MinStep)
            return false;                                  // 挪不动了

        next = candidate;
        return true;
    }
}
