using System.Collections.Generic;
using System.Linq;
using EndfieldCharge.Contracts;
using EndfieldCharge.Services;

namespace EndfieldCharge.Host.Plugins;

/// <summary>
/// 进程内插件注册表（Phase 3 只做内存版，不涉及动态加载）。
/// </summary>
public sealed class PluginRegistry : IPluginRegistry
{
    private readonly object _gate = new();
    private readonly List<IPlugin> _plugins = new();

    public IReadOnlyList<IPlugin> Plugins
    {
        get
        {
            lock (_gate)
                return _plugins.ToArray();
        }
    }

    public IEnumerable<IContextMenuContributor> Contributors => Plugins.OfType<IContextMenuContributor>();

    public void Register(IPlugin plugin)
    {
        lock (_gate)
        {
            if (_plugins.Any(p => p.Id == plugin.Id))
            {
                Logger.Warn($"PluginRegistry: 插件 {plugin.Id} 已注册，忽略重复注册");
                return;
            }

            _plugins.Add(plugin);
        }
    }

    public T? Resolve<T>() where T : class => Plugins.OfType<T>().FirstOrDefault();

    public IEnumerable<T> ResolveAll<T>() where T : class => Plugins.OfType<T>();

    public bool Remove(string id)
    {
        lock (_gate)
        {
            var plugin = _plugins.FirstOrDefault(p => p.Id == id);
            if (plugin is null)
                return false;

            // 元插件（battery.meta）与任何 CanUnload=false 的插件都不可移除
            if (!plugin.CanUnload || plugin.Id == "battery.meta")
            {
                Logger.Warn($"PluginRegistry: 插件 {id} 不可卸载（CanUnload=false），拒绝移除");
                return false;
            }

            plugin.Shutdown();
            return _plugins.Remove(plugin);
        }
    }
}
