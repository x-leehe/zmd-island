using System;
using System.Collections.Generic;
using Avalonia.Media.Imaging;

namespace EndfieldCharge.Host.Plugins.Music;

/// <summary>岛内音乐按钮动作（皮肤只发出请求，真正播放由音乐模块实现）。</summary>
public enum MusicAction
{
    PlayPause,
    Previous,
    Next,
}

/// <summary>
/// 音乐皮肤的一帧内容。骨架阶段由 <see cref="Demo"/> 假数据填充；
/// 后续由 SMTC（曲目/封面/进度）、歌词模块、频谱模块填充。
/// </summary>
public sealed record MusicFrame
{
    public string? Title { get; init; }

    public string? Artist { get; init; }

    /// <summary>专辑名（参与 LRCLIB 的精确匹配）。</summary>
    public string? Album { get; init; }

    /// <summary>曲目总时长：歌词按绝对时间取行，LRCLIB 也用它区分同名曲。</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>来源应用的 AUMID（频谱白名单按它判定；为空表示未知，一律视为不在白名单）。</summary>
    public string? SourceAppId { get; init; }

    public string? LyricCurrent { get; init; }

    /// <summary>播放进度 0..1。</summary>
    public double Progress { get; init; }

    public bool IsPlaying { get; init; }

    /// <summary>专辑封面（SMTC 缩略图；无则为 null）。</summary>
    public Bitmap? Cover { get; init; }

    public static MusicFrame Demo { get; } = new()
    {
        Title = "曲目名称",
        Artist = "歌手 · 专辑",
        LyricCurrent = "正在演唱的这一句",
        Progress = 0.45,
        IsPlaying = false,
    };

    /// <summary>空态：当前没有媒体会话（不显示任何假数据，封面位露出音符占位字形）。</summary>
    public static MusicFrame Empty { get; } = new()
    {
        Title = null,
        Artist = string.Empty,
        LyricCurrent = string.Empty,
        Progress = 0d,
        IsPlaying = false,
    };

    /// <summary>降级态：SMTC 连接重试已用尽，岛内明确提示（而不是假装在放歌）。</summary>
    public static MusicFrame Unavailable() => new()
    {
        Title = null,
        Artist = string.Empty,
        LyricCurrent = Localization.MediaUnavailable,
        Progress = 0d,
        IsPlaying = false,
    };
}
