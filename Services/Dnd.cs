using System;
using EndfieldCharge.Contracts;
using EndfieldCharge.Settings;

namespace EndfieldCharge.Services;

/// <summary>免打扰档位（右键菜单「免打扰 ▸」）。</summary>
public enum DndOption
{
    /// <summary>取消免打扰。</summary>
    Off,

    FiveMinutes,

    FifteenMinutes,

    OneHour,

    ThreeHours,

    /// <summary>暂停：一直生效，直到手动取消。</summary>
    Pause,
}

/// <summary>
/// 免打扰（**整岛生效**，不是某个插件的事）：
/// 只拦「主动弹出」——插拔电响应态、省电提示、插件请求展开；<strong>不拦</strong>用户手势
/// （托盘显示 / 滚轮翻页 / 悬停唤醒）。
/// <para>
/// 纯逻辑：不碰 UI、不用定时器。是否生效 = 当前时刻与截止时刻比较；
/// 状态存在 <see cref="AppSettings"/> 里（随设置持久化）。
/// </para>
/// </summary>
public static class Dnd
{
    /// <summary>菜单中列出的档位（顺序即展示顺序）。</summary>
    public static readonly DndOption[] MenuOptions =
    {
        DndOption.FiveMinutes,
        DndOption.FifteenMinutes,
        DndOption.OneHour,
        DndOption.ThreeHours,
        DndOption.Pause,
    };

    /// <summary>当前是否处于免打扰。</summary>
    public static bool IsActive(AppSettings settings, DateTime nowUtc) =>
        settings.DndPaused || (settings.DndUntilUtc is { } until && until > nowUtc);

    /// <summary>按档位算出新的设置（纯函数：返回改过的副本，由调用方持久化）。</summary>
    public static AppSettings Apply(AppSettings settings, DndOption option, DateTime nowUtc) => option switch
    {
        DndOption.Off => settings with { DndUntilUtc = null, DndPaused = false },
        DndOption.Pause => settings with { DndUntilUtc = null, DndPaused = true },
        _ => settings with { DndUntilUtc = nowUtc + Duration(option), DndPaused = false },
    };

    /// <summary>剩余时间（暂停或未生效时为 <c>null</c>）。</summary>
    public static TimeSpan? Remaining(AppSettings settings, DateTime nowUtc)
    {
        if (settings.DndPaused)
            return null;

        if (settings.DndUntilUtc is not { } until || until <= nowUtc)
            return null;

        return until - nowUtc;
    }

    /// <summary>档位对应的时长（<see cref="DndOption.Off"/> / <see cref="DndOption.Pause"/> 为 0）。</summary>
    public static TimeSpan Duration(DndOption option) => option switch
    {
        DndOption.FiveMinutes => TimeSpan.FromMinutes(5),
        DndOption.FifteenMinutes => TimeSpan.FromMinutes(15),
        DndOption.OneHour => TimeSpan.FromHours(1),
        DndOption.ThreeHours => TimeSpan.FromHours(3),
        _ => TimeSpan.Zero,
    };

    /// <summary>档位在菜单里的文案。</summary>
    public static string Label(DndOption option) => option switch
    {
        DndOption.Off => Localization.DndOff,
        DndOption.FiveMinutes => Localization.Dnd5Min,
        DndOption.FifteenMinutes => Localization.Dnd15Min,
        DndOption.OneHour => Localization.Dnd1Hour,
        DndOption.ThreeHours => Localization.Dnd3Hour,
        _ => Localization.DndPause,
    };

    /// <summary>「免打扰」父条目的文案：生效时带上剩余量，一眼能看到状态。</summary>
    public static string Describe(AppSettings settings, DateTime nowUtc)
    {
        if (!IsActive(settings, nowUtc))
            return Localization.Dnd;

        if (settings.DndPaused)
            return $"{Localization.Dnd} · {Localization.DndPaused}";

        var remaining = Remaining(settings, nowUtc) ?? TimeSpan.Zero;
        return remaining.TotalHours >= 1d
            ? $"{Localization.Dnd} · {Localization.DndRemainingHours((int)remaining.TotalHours, remaining.Minutes)}"
            : $"{Localization.Dnd} · {Localization.DndRemainingMinutes((int)Math.Ceiling(remaining.TotalMinutes))}";
    }
}
