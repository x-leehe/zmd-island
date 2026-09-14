using System;
using System.Collections.Generic;

namespace EndfieldCharge.Host.Plugins.Music;

/// <summary>
/// 「在白名单里挑会话」的纯逻辑（不碰 WinRT，可单测）。
/// <para>
/// 白名单是**硬门禁**：只有允许来源的会话才有资格被显示 / 被控制 —— 取不到 AUMID（来源未知）
/// 一律视为不允许。挑选优先级与旧行为一致，只是每一步都先过白名单：
/// 当前且在播 → 任意在播 → 当前 → 任意允许；全都不允许则返回 <c>-1</c>（此时调用方应显示空态，
/// 而不是回退到未授权的当前会话）。
/// </para>
/// </summary>
public static class SessionSelection
{
    /// <summary>一个候选会话的可判定字段（由调用方从 SMTC 会话读出）。</summary>
    public readonly record struct Candidate(string? AppId, bool IsPlaying, bool IsCurrent);

    /// <summary>返回选中的候选下标；没有任何被允许的会话时返回 <c>-1</c>。</summary>
    public static int Pick(IReadOnlyList<Candidate> candidates, IReadOnlyList<string>? whitelist)
    {
        return First(candidates, whitelist, static c => c.IsCurrent && c.IsPlaying)
            ?? First(candidates, whitelist, static c => c.IsPlaying)
            ?? First(candidates, whitelist, static c => c.IsCurrent)
            ?? First(candidates, whitelist, static _ => true)
            ?? -1;
    }

    private static int? First(
        IReadOnlyList<Candidate> candidates,
        IReadOnlyList<string>? whitelist,
        Func<Candidate, bool> predicate)
    {
        for (int i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            if (predicate(candidate) && SpectrumWhitelist.IsAllowed(whitelist, candidate.AppId))
                return i;
        }

        return null;
    }
}
