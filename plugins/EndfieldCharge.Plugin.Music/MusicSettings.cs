using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using EndfieldCharge.Services;

namespace EndfieldCharge.Host.Plugins.Music;

/// <summary>音乐插件的设置（持久化在插件自己的数据目录里，宿主不参与其语义）。</summary>
public sealed record MusicSettings
{
    /// <summary>展开态超时（秒）：最低 3（宿主与插件都会夹紧，不允许"没有超时"）。</summary>
    public double ExpandedTimeoutSeconds { get; init; } = 6.0;

    /// <summary>无歌词时用曲名占位（等待态那一行）。</summary>
    public bool ShowTitleWhenNoLyric { get; init; } = true;

    /// <summary>显示可视化器（频谱柱 / 包络）。</summary>
    public bool ShowVisualizer { get; init; } = true;

    /// <summary>
    /// 歌词来源：<c>merge</c>（默认：三源并行择优） / <c>prefer-lrclib</c> / <c>prefer-netease</c> /
    /// <c>prefer-local</c>（这三种 = 串行回退，排第一的优先） / <c>lrclib</c> / <c>netease</c> / <c>local</c>（仅这一源） /
    /// <c>off</c>（关闭）。旧值 <c>auto</c> / <c>smtc</c> 由 <see cref="LyricsService"/> 迁移为 <c>merge</c>。
    /// </summary>
    public string LyricSource { get; init; } = "merge";

    /// <summary>
    /// 音乐来源白名单（应用 AUMID）。**默认空 ⇒ 不采集频谱、也不主动弹岛**。
    /// 这是硬约束：白名单是采集的**前置条件**，不是事后过滤 —— 不在名单里的来源根本不会建立采集链路。
    /// </summary>
    public IReadOnlyList<string> SpectrumSources { get; init; } = Array.Empty<string>();
}

/// <summary>音乐插件设置的读写：数据目录下的 settings.json；目录不可用时退化为内存态。</summary>
public static class MusicSettingsStore
{
    private const string FileName = "settings.json";

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static MusicSettings Load(string dataDirectory)
    {
        if (string.IsNullOrEmpty(dataDirectory))
            return new MusicSettings();

        try
        {
            string path = Path.Combine(dataDirectory, FileName);
            if (!File.Exists(path))
                return new MusicSettings();

            return JsonSerializer.Deserialize<MusicSettings>(File.ReadAllText(path)) ?? new MusicSettings();
        }
        catch (Exception ex)
        {
            Logger.Warn($"Music: 设置读取失败，改用默认值 —— {ex.Message}");
            return new MusicSettings();
        }
    }

    public static void Save(string dataDirectory, MusicSettings settings)
    {
        if (string.IsNullOrEmpty(dataDirectory))
            return;

        try
        {
            File.WriteAllText(
                Path.Combine(dataDirectory, FileName),
                JsonSerializer.Serialize(settings, Options));
        }
        catch (Exception ex)
        {
            Logger.Warn($"Music: 设置保存失败 —— {ex.Message}");
        }
    }
}
