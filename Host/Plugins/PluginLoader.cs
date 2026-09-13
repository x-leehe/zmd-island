using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using EndfieldCharge.Contracts;
using EndfieldCharge.Services;

namespace EndfieldCharge.Host.Plugins;

/// <summary>
/// 外部插件装载器：扫描宿主旁 <c>plugins/*.dll</c>（契约程序集除外）。
/// 每个插件独立 <see cref="AssemblyLoadContext"/>（<see cref="AssemblyDependencyResolver"/>
/// 解析其私有依赖）；**契约程序集留在默认上下文**，否则插件与宿主会各自加载一份契约，
/// <c>is IPlugin</c> 判断将因类型标识分裂而恒为 false。
/// 加载失败只跳过并记日志，不影响宿主启动。
/// </summary>
public static class PluginLoader
{
    /// <summary>必须走默认上下文的程序集（契约 + 框架）。</summary>
    internal static readonly HashSet<string> SharedAssemblies = new(StringComparer.OrdinalIgnoreCase)
    {
        "EndfieldCharge.Contracts",
        "EndfieldCharge.Contracts.Avalonia",
        "Avalonia",
        "Avalonia.Base",
    };

    /// <param name="pluginsDirectory">插件目录（宿主旁 <c>plugins/</c>）。</param>
    /// <param name="serviceResolver">宿主服务解析器（供 <see cref="IPluginContext.GetService{T}"/>），可空。</param>
    public static IReadOnlyList<IPlugin> Load(string pluginsDirectory, Func<Type, object?>? serviceResolver = null)
    {
        var result = new List<IPlugin>();
        if (!Directory.Exists(pluginsDirectory))
        {
            Logger.Info($"PluginLoader: 未找到插件目录 {pluginsDirectory}（跳过）");
            return result;
        }

        foreach (var dll in Directory.EnumerateFiles(pluginsDirectory, "*.dll", SearchOption.TopDirectoryOnly))
        {
            // 契约程序集若被一并放在 plugins/ 下则跳过（必须走默认上下文，不能从插件目录加载）
            var fileName = Path.GetFileNameWithoutExtension(dll);
            if (fileName.StartsWith("EndfieldCharge.Contracts", StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                var context = new PluginLoadContext(Path.GetFullPath(dll));
                var assembly = context.LoadFromAssemblyPath(Path.GetFullPath(dll));

                foreach (var type in assembly.GetTypes())
                {
                    if (type.IsAbstract || !typeof(IPlugin).IsAssignableFrom(type))
                        continue;

                    if (Activator.CreateInstance(type) is not IPlugin plugin)
                        continue;

                    // 生命周期接线：先给上下文（数据目录 + 宿主服务），初始化失败则跳过该插件
                    try
                    {
                        plugin.Initialize(PluginContext.Create(plugin.Id, serviceResolver));
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"PluginLoader: 插件 {plugin.Id} 初始化失败，跳过 —— {ex.Message}");
                        continue;
                    }

                    result.Add(plugin);
                    Logger.Info($"PluginLoader: 已加载外部插件 {plugin.Id}（{Path.GetFileName(dll)}）");
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"PluginLoader: 跳过 {Path.GetFileName(dll)} —— {ex.Message}");
            }
        }

        return result;
    }
}

/// <summary>单插件加载上下文：契约走默认上下文，其余依赖由插件目录旁的 deps.json 解析。</summary>
internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginPath) : base(isCollectible: false)
        => _resolver = new AssemblyDependencyResolver(pluginPath);

    protected override Assembly? Load(AssemblyName name)
    {
        if (PluginLoader.SharedAssemblies.Contains(name.Name ?? string.Empty))
            return null; // 返回 null = 交回默认上下文（共享宿主已加载的契约）

        var path = _resolver.ResolveAssemblyToPath(name);
        return path is null ? null : LoadFromAssemblyPath(path);
    }
}
