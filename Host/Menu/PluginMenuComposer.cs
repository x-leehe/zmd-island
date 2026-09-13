using System.Collections.Generic;
using System.Linq;
using EndfieldCharge.Contracts;
using EndfieldCharge.Host.Plugins;

namespace EndfieldCharge.Host.Menu;

/// <summary>
/// 菜单合成器（无 Avalonia 依赖，纯数据）：
///
/// 合成顺序（每个目标菜单）：
///   1. C 类——插件注入内容区（Section == Content，按 Priority 升序）；
///   2. B 类——插件声明的宿主固定区前置项（Section == HostFixed，按 Priority 升序）；
///   3. 若已有内容区且宿主固定项非空 → 一条分隔线；
///   4. A 类——宿主固定项（hostFixed，按传入顺序）。
///
/// 「插件 ▸」子菜单行由 BuildPluginChildren 从注册表生成（宿主负责添加该条目）：
/// 每个已注册插件一行，Header = DisplayName，IsChecked = true（启用占位），
/// IsEnabled = plugin.CanUnload——元插件（CanUnload=false，如 battery.meta）锁定/禁用。
/// </summary>
public static class PluginMenuComposer
{
    public static IReadOnlyList<MenuContribution> Compose(
        MenuTarget target,
        IPluginRegistry registry,
        IEnumerable<MenuContribution> hostFixed)
    {
        var contributions = registry.Contributors
            .SelectMany(c => c.GetMenuContributions(new MenuContext { Target = target }))
            .Where(m => m.Target == target && m.IsVisible)
            .ToList();

        var content = contributions
            .Where(m => m.Section == MenuSection.Content)
            .OrderBy(m => m.Priority)
            .ToList();

        var pluginHostFixed = contributions
            .Where(m => m.Section == MenuSection.HostFixed)
            .OrderBy(m => m.Priority)
            .ToList();

        var host = hostFixed.Where(m => m.IsVisible).ToList();

        var result = new List<MenuContribution>(content.Count + pluginHostFixed.Count + host.Count + 1);
        result.AddRange(content);
        result.AddRange(pluginHostFixed);

        // 内容区与宿主固定项之间的分隔线
        if (result.Count > 0 && host.Count > 0)
        {
            result.Add(new MenuContribution
            {
                Id = $"{target}.separator.content-host",
                Header = string.Empty,
                Target = target,
                Section = MenuSection.HostFixed,
                IsSeparator = true,
            });
        }

        result.AddRange(host);
        return result;
    }

    /// <summary>
    /// 「插件 ▸」子菜单行：由注册表生成，一个插件一行。
    /// 元插件（CanUnload=false）→ IsEnabled=false（锁定，不可切换）。
    /// </summary>
    public static IReadOnlyList<MenuContribution> BuildPluginChildren(MenuTarget target, IPluginRegistry registry)
        => registry.Plugins
            .OrderBy(p => p.Id)
            .Select(p => new MenuContribution
            {
                Id = $"plugins.managed.{p.Id}",
                Header = p.DisplayName,
                Target = target,
                Section = MenuSection.HostFixed,
                IsChecked = true,          // 启用占位（v1 无真实启用/禁用状态）
                IsEnabled = p.CanUnload,   // 元插件锁定
            })
            .ToArray();
}
