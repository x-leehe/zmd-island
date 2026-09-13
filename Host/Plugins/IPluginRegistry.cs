using System.Collections.Generic;
using EndfieldCharge.Contracts;

namespace EndfieldCharge.Host.Plugins;

/// <summary>进程内插件注册表契约。</summary>
public interface IPluginRegistry
{
    /// <summary>已注册插件快照。</summary>
    IReadOnlyList<IPlugin> Plugins { get; }

    void Register(IPlugin plugin);

    /// <summary>解析第一个可赋值给 T 的插件；无则 null。</summary>
    T? Resolve<T>() where T : class;

    /// <summary>解析所有可赋值给 T 的插件。</summary>
    IEnumerable<T> ResolveAll<T>() where T : class;

    /// <summary>
    /// 移除插件。CanUnload == false（含 battery.meta 元插件）时拒绝并返回 false。
    /// </summary>
    bool Remove(string id);

    /// <summary>所有实现 IContextMenuContributor 的插件。</summary>
    IEnumerable<IContextMenuContributor> Contributors { get; }
}
