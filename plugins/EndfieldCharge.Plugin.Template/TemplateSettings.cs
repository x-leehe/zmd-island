using System;
using System.IO;
using System.Text.Json;
using EndfieldCharge.Services;

namespace EndfieldCharge.Host.Plugins.Template;

/// <summary>
/// 模板插件的设置（持久化在插件自己的数据目录里，宿主不参与其语义）。
/// 四类字段刻意覆盖 bool / number / choice / string 四种设置形态。
/// </summary>
public sealed record TemplateSettings
{
    /// <summary>示例开关（bool）。</summary>
    public bool Flag { get; init; } = true;

    /// <summary>示例数值（number）：与设置页的滑块 / 步进器联动，范围 0.5–3.0。</summary>
    public double Number { get; init; } = 1.5;

    /// <summary>示例选项（choice）：<c>alpha</c> / <c>beta</c> / <c>gamma</c>。</summary>
    public string Choice { get; init; } = "alpha";

    /// <summary>示例文本（string）。</summary>
    public string Text { get; init; } = string.Empty;
}

/// <summary>模板插件设置的读写：数据目录下的 settings.json；目录不可用时退化为内存态。</summary>
public static class TemplateSettingsStore
{
    private const string FileName = "settings.json";

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static TemplateSettings Load(string dataDirectory)
    {
        if (string.IsNullOrEmpty(dataDirectory))
            return new TemplateSettings();

        try
        {
            string path = Path.Combine(dataDirectory, FileName);
            if (!File.Exists(path))
                return new TemplateSettings();

            return JsonSerializer.Deserialize<TemplateSettings>(File.ReadAllText(path)) ?? new TemplateSettings();
        }
        catch (Exception ex)
        {
            Logger.Warn($"Template: 设置读取失败，改用默认值 —— {ex.Message}");
            return new TemplateSettings();
        }
    }

    public static void Save(string dataDirectory, TemplateSettings settings)
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
            Logger.Warn($"Template: 设置保存失败 —— {ex.Message}");
        }
    }
}
