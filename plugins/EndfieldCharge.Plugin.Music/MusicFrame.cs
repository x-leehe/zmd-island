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

    public string? LyricCurrent { get; init; }

    /// <summary>播放进度 0..1。</summary>
    public double Progress { get; init; }

    public bool IsPlaying { get; init; }

    /// <summary>频谱柱值 0..1（平滑包络；骨架为多正弦叠加）。</summary>
    public IReadOnlyList<float>? Spectrum { get; init; }

    /// <summary>专辑封面（SMTC 缩略图；无则为 null）。</summary>
    public Bitmap? Cover { get; init; }

    public static MusicFrame Demo { get; } = new()
    {
        Title = "曲目名称",
        Artist = "歌手 · 专辑",
        LyricCurrent = "正在演唱的这一句",
        Progress = 0.45,
        IsPlaying = false,
        Spectrum = BuildDemoSpectrum(40),
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

    /// <summary>确定性平滑包络（多正弦叠加），与画布稿的「平滑过渡」观感一致。</summary>
    private static float[] BuildDemoSpectrum(int n)
    {
        var outv = new float[n];
        for (int i = 0; i < n; i++)
        {
            double t = n == 1 ? 0.5d : (double)i / (n - 1);
            double v = 0.44
                + 0.30 * Math.Sin(t * Math.PI * 2 * 1.15 + 0.6)
                + 0.14 * Math.Sin(t * Math.PI * 2 * 2.70 + 2.1)
                + 0.08 * Math.Sin(t * Math.PI * 2 * 5.30 + 4.4);
            outv[i] = (float)Math.Clamp(v, 0.08d, 1d);
        }
        return outv;
    }
}
