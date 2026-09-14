using System;
using System.Collections.Generic;
using EndfieldCharge.Host.Plugins.Music;
using Xunit;

namespace EndfieldCharge.Tests;

/// <summary>
/// 频谱内核：静音为 0、正弦落在正确频段、低频在左高频在右（对数分带）、起落平滑且**与帧率无关**。
/// 纯计算，不碰音频设备。
/// </summary>
public class SpectrumAnalyzerTests
{
    private const int Rate = 48000;
    private const int Size = 1024;

    /// <summary>默认按 60fps 推进（时间常数决定手感，帧率只影响调用次数）。</summary>
    private const double Dt = 1d / 60d;

    private static float[] Sine(double frequency, float amplitude = 0.5f)
    {
        var samples = new float[Size];
        for (int i = 0; i < Size; i++)
            samples[i] = amplitude * MathF.Sin(2f * MathF.PI * (float)frequency * i / Rate);

        return samples;
    }

    private static int PeakBand(IReadOnlyList<float> levels)
    {
        int best = 0;
        for (int i = 1; i < levels.Count; i++)
        {
            if (levels[i] > levels[best])
                best = i;
        }

        return best;
    }

    [Fact]
    public void Silence_stays_at_zero()
    {
        var analyzer = new SpectrumAnalyzer(Size, 40, Rate);

        for (int frame = 0; frame < 5; frame++)
        {
            var levels = analyzer.Process(new float[Size], Dt);

            Assert.Equal(40, levels.Count);
            foreach (var level in levels)
                Assert.Equal(0f, level, 3);
        }
    }

    [Fact]
    public void Sine_lights_up_the_band_that_contains_it()
    {
        var analyzer = new SpectrumAnalyzer(Size, 40, Rate);

        // 多喂几帧让平滑爬上去
        IReadOnlyList<float> levels = Array.Empty<float>();
        for (int frame = 0; frame < 30; frame++)
            levels = analyzer.Process(Sine(1000d), Dt);

        int peak = PeakBand(levels);
        Assert.InRange(peak, 1, levels.Count - 1);
        Assert.True(levels[peak] > 0.4f, $"峰值频段太弱：{levels[peak]}");
    }

    [Fact]
    public void Higher_frequency_lands_in_a_higher_band()
    {
        var analyzer = new SpectrumAnalyzer(Size, 40, Rate);

        IReadOnlyList<float> low = Array.Empty<float>();
        for (int frame = 0; frame < 30; frame++)
            low = analyzer.Process(Sine(200d), Dt);

        int lowPeak = PeakBand(low);

        var analyzer2 = new SpectrumAnalyzer(Size, 40, Rate);
        IReadOnlyList<float> high = Array.Empty<float>();
        for (int frame = 0; frame < 30; frame++)
            high = analyzer2.Process(Sine(6000d), Dt);

        int highPeak = PeakBand(high);

        Assert.True(highPeak > lowPeak, $"6kHz 应落在更低频右侧：low={lowPeak}, high={highPeak}");
    }

    [Fact]
    public void Levels_rise_and_fall_smoothly()
    {
        var analyzer = new SpectrumAnalyzer(Size, 40, Rate);

        // 上升是有惯性的：一帧就冲到高位就不叫"平缓"了
        // （这条断言就是拦住有人再把起跳速度调回"啪"地弹上去的手感）
        var first = analyzer.Process(Sine(1000d), Dt);
        int band = PeakBand(first);
        Assert.True(first[band] < 0.5f, $"第一帧就冲太高，起落不平缓：{first[band]}");

        // 喂到高位（惯性收敛）
        IReadOnlyList<float> loud = first;
        for (int frame = 0; frame < 60; frame++)
            loud = analyzer.Process(Sine(1000d), Dt);

        float top = loud[band];
        Assert.True(top > 0.3f, $"收敛后应当有可见柱高：{top}");

        // 一帧静音：只轻微回落（不会立刻掉到 0）
        var afterOne = analyzer.Process(new float[Size], Dt);
        Assert.True(afterOne[band] > top * 0.5f, $"一帧就掉了太多：{top} → {afterOne[band]}");

        // 连续衰减：单调下降且最终贴地
        float previous = afterOne[band];
        float last = previous;
        for (int frame = 0; frame < 300; frame++)
        {
            last = analyzer.Decay(Dt)[band];
            Assert.True(last <= previous + 1e-6f, "衰减过程中不应回升");
            previous = last;
        }

        Assert.True(last < 0.01f, $"应当衰减到近乎静止：{last}");
    }

    [Fact]
    public void Levels_do_not_depend_on_the_frame_rate()
    {
        // 同样推进 0.5 秒：30fps 喂 15 帧、60fps 喂 30 帧 —— 平滑按**时间**推进，结果应该几乎一致。
        // （这就是"把每帧比例换成时间常数"的意义：帧率只影响画得顺不顺，不影响手感。）
        var slow = new SpectrumAnalyzer(Size, 40, Rate);
        IReadOnlyList<float> a = Array.Empty<float>();
        for (int frame = 0; frame < 15; frame++)
            a = slow.Process(Sine(1000d), 1d / 30d);

        var fast = new SpectrumAnalyzer(Size, 40, Rate);
        IReadOnlyList<float> b = Array.Empty<float>();
        for (int frame = 0; frame < 30; frame++)
            b = fast.Process(Sine(1000d), 1d / 60d);

        Assert.InRange(MathF.Abs(a[PeakBand(a)] - b[PeakBand(b)]), 0f, 0.02f);
    }

    [Fact]
    public void Tonal_signal_still_shows_contrast()
    {
        // 不能压成"顶着岛顶的大平原"—— 乐音所在段必须明显高于空白段。
        // （S 形曲线的形状本身由 SpectrumCurveTests 直接测；这里只管"别摊平"。）
        var analyzer = new SpectrumAnalyzer(Size, 40, Rate);

        var tonal = new float[Size];
        for (int i = 0; i < Size; i++)
        {
            double t = (double)i / Rate;
            tonal[i] = (float)(
                (0.30d * Math.Sin(2d * Math.PI * 120d * t)) +
                (0.25d * Math.Sin(2d * Math.PI * 900d * t)) +
                (0.20d * Math.Sin(2d * Math.PI * 3000d * t)) +
                (0.15d * Math.Sin(2d * Math.PI * 9000d * t)));
        }

        IReadOnlyList<float> levels = Array.Empty<float>();
        for (int frame = 0; frame < 90; frame++)
            levels = analyzer.Process(tonal, Dt);

        float min = float.MaxValue;
        float max = 0f;
        foreach (var level in levels)
        {
            min = Math.Min(min, level);
            max = Math.Max(max, level);
        }

        Assert.True(max - min > 0.2f, $"压得太平，已经没有起伏了：min={min}, max={max}");
    }

    [Fact]
    public void Pink_noise_does_not_lean_left()
    {
        // 「弹道偏左」的量化口径：音乐型信号（1/f，低端天生强）经倾斜补偿后应当大致持平。
        // 去掉那条 +3dB/oct 补偿，左半均值会明显压过右半。
        var analyzer = new SpectrumAnalyzer(Size, 40, Rate);
        var random = new Random(20260914);
        var pink = new float[Size];
        double lowPass = 0d;

        for (int i = 0; i < Size; i++)
        {
            lowPass = (0.97d * lowPass) + (0.03d * ((random.NextDouble() * 2d) - 1d));
            pink[i] = (float)(lowPass * 3d);
        }

        IReadOnlyList<float> levels = Array.Empty<float>();
        for (int frame = 0; frame < 90; frame++)
            levels = analyzer.Process(pink, Dt);

        int half = levels.Count / 2;
        double left = 0d;
        double right = 0d;
        for (int i = 0; i < levels.Count; i++)
        {
            if (i < half)
                left += levels[i];
            else
                right += levels[i];
        }

        left /= half;
        right /= levels.Count - half;

        Assert.True(left - right < 0.25d, $"频谱整体偏左：左均={left:F3} 右均={right:F3}");
    }

    [Fact]
    public void Process_rejects_wrong_sample_count()
    {
        var analyzer = new SpectrumAnalyzer(Size, 40, Rate);

        Assert.Throws<ArgumentException>(() => analyzer.Process(new float[Size / 2], Dt));
    }

    [Theory]
    [InlineData(512)]
    [InlineData(2048)]
    public void Fft_size_must_be_a_power_of_two(int size)
    {
        var analyzer = new SpectrumAnalyzer(size, 40, Rate);

        Assert.Equal(size, analyzer.FftSize);
        Assert.Throws<ArgumentOutOfRangeException>(() => new SpectrumAnalyzer(1000, 40, Rate));
    }

    [Fact]
    public void Band_count_is_respected()
    {
        var analyzer = new SpectrumAnalyzer(Size, 12, Rate);

        Assert.Equal(12, analyzer.Bands);
        Assert.Equal(12, analyzer.Process(Sine(1000d), Dt).Count);
        Assert.Equal(12, analyzer.Decay(Dt).Count);
    }
}
