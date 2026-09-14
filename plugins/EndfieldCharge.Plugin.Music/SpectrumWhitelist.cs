using System;
using System.Collections.Generic;

namespace EndfieldCharge.Host.Plugins.Music;

/// <summary>
/// 音频来源白名单（按应用 AUMID）：<strong>只有名单内的来源才会被采集频谱，也才会触发「开始播放自动弹岛」</strong>。
/// <para>
/// 三条硬规矩：
/// <list type="number">
///   <item>默认空名单 ⇒ **一律不允许**（不是"默认全允许"）：装上去之后不监听任何来源，用户显式加入才开；</item>
///   <item>白名单是采集的**前置条件**，不是事后过滤 —— 不在名单里就根本不建立采集链路；</item>
///   <item>取不到 AUMID（来源未知）一律视为**不在名单**。</item>
/// </list>
/// </para>
/// </summary>
public static class SpectrumWhitelist
{
    /// <summary>该来源是否被允许（忽略大小写；空名单、空 AUMID 都返回 <c>false</c>）。</summary>
    public static bool IsAllowed(IReadOnlyList<string>? sources, string? appId)
    {
        if (string.IsNullOrWhiteSpace(appId) || sources is null || sources.Count == 0)
            return false;

        var wanted = appId.Trim();
        foreach (var source in sources)
        {
            if (string.IsNullOrWhiteSpace(source))
                continue;

            if (string.Equals(source.Trim(), wanted, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
