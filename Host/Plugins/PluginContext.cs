using System;
using System.IO;
using EndfieldCharge.Contracts;
using EndfieldCharge.Services;

namespace EndfieldCharge.Host.Plugins;

/// <summary>
/// 宿主为每个插件创建的 <see cref="IPluginContext"/>：
/// 每插件数据目录（<c>%APPDATA%\EndfieldCharge\plugins\&lt;id&gt;</c>，失败退回 TEMP）
/// + 懒解析的宿主服务（<see cref="IIslandHost"/> 等）。
/// 服务用委托而非字典：HudWindow 在插件加载之后才创建，插件可在需要时再取。
/// </summary>
public sealed class PluginContext : IPluginContext
{
    private readonly Func<Type, object?> _resolve;

    private PluginContext(string dataDirectory, Func<Type, object?> resolve)
    {
        DataDirectory = dataDirectory;
        _resolve = resolve;
    }

    public string DataDirectory { get; }

    public T? GetService<T>() where T : class => _resolve(typeof(T)) as T;

    /// <summary>创建上下文并确保数据目录存在；创建失败退回临时目录，仍失败则退回空串。</summary>
    public static PluginContext Create(string pluginId, Func<Type, object?>? resolve = null)
    {
        string dir;
        try
        {
            dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EndfieldCharge", "plugins", pluginId);
            Directory.CreateDirectory(dir);
        }
        catch (Exception ex)
        {
            Logger.Warn($"PluginContext: 无法创建数据目录（{pluginId}）—— {ex.Message}；退回临时目录");
            try
            {
                dir = Path.Combine(Path.GetTempPath(), "EndfieldCharge", "plugins", pluginId);
                Directory.CreateDirectory(dir);
            }
            catch
            {
                dir = string.Empty;
            }
        }

        return new PluginContext(dir, resolve ?? (_ => null));
    }
}
