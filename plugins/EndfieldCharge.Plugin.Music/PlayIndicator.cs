using System;

namespace EndfieldCharge.Host.Plugins.Music;

/// <summary>
/// 「正在播放」指示器的假动画：等待态 / 收缩态那三根柱**不接真实采样**，
/// 只按相位算一组错相的正弦 —— 它标示的是「音乐在播放」，不是「音频长什么样」。
/// <para>
/// 三根柱要表达细节本来就不够（真实采样驱动它既看不出来，又得一直占着采集链路），
/// 所以真实频谱只出现在展开态的背景层（那里有 40 段，够表达）。
/// </para>
/// </summary>
public static class PlayIndicator
{
    /// <summary>柱高（0..1）。<paramref name="playing"/> 为 false 时全为 0（暂停就不标示"在播放"）。</summary>
    public static float[] Build(bool playing, double phase, int bars = 3)
    {
        var levels = new float[Math.Max(1, bars)];
        if (!playing)
            return levels;

        for (int i = 0; i < levels.Length; i++)
        {
            double value = 0.42d
                + (0.34d * Math.Sin((phase * 2.1d) + (i * 0.9d)))
                + (0.12d * Math.Sin((phase * 4.7d) + (i * 1.7d)));

            levels[i] = (float)Math.Clamp(value, 0.08d, 1d);
        }

        return levels;
    }
}
