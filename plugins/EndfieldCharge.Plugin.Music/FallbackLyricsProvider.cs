using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EndfieldCharge.Services;

namespace EndfieldCharge.Host.Plugins.Music;

/// <summary>
/// 串行回退（「偏好 X」用）：按顺序问各来源，**第一个给出可用歌词的就采用**，后面的不再问。
/// <para>
/// 与并行择优（<see cref="MergedLyricsProvider"/>）的区别：这里不比较"谁更像"，只认先后顺序 ——
/// 「偏好 X」就是"把 X 排在第一位的串行检索"，这里不关心另一个源是不是给了更像的结果。
/// </para>
/// </summary>
public sealed class FallbackLyricsProvider : ILyricsProvider
{
    private readonly IReadOnlyList<ILyricsProvider> _providers;

    public FallbackLyricsProvider(params ILyricsProvider[] providers)
        => _providers = providers ?? Array.Empty<ILyricsProvider>();

    /// <summary>参与回退的来源顺序（日志 / 排查用）。</summary>
    public IReadOnlyList<ILyricsProvider> Providers => _providers;

    /// <inheritdoc />
    public async Task<LrcTimeline?> FetchAsync(LyricsQuery query, CancellationToken ct)
    {
        foreach (var provider in _providers)
        {
            if (ct.IsCancellationRequested)
                return null;

            LrcTimeline? timeline;
            try
            {
                timeline = await provider.FetchAsync(query, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception ex)
            {
                // 单个来源出错不该拖垮整条链：记一行，继续问下一个
                Logger.Warn($"Music: 歌词源 {provider.GetType().Name} 失败 —— {ex.GetType().Name} {ex.Message}");
                continue;
            }

            if (timeline is { IsEmpty: false })
                return timeline;
        }

        return null;
    }
}
