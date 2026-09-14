using System;
using System.Collections.Generic;
using System.Linq;
using EndfieldCharge.Contracts;
using EndfieldCharge.Host.Island;
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
/// 「插件 ▸」子菜单行由 BuildPanelChildren 生成（宿主负责添加该条目）：
/// 列出所有**声明了岛面板**的插件（实现 IIslandSkin）；参与滚轮轮转的项可用、点击即切换；
/// 明确声明滚轮找不到的（ParticipatesInWheelSwitch=false）项**置灰不可点**——让用户知道这里确实有个插件，
/// 但不会把不该被切到的面板切出来。
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
    /// 「插件 ▸」子菜单行：列出所有声明了岛面板的插件。
    /// <para>
    /// <paramref name="switchableIds"/> = 参与滚轮轮转的面板；在此集合内的项可用、点击即切换；
    /// 不在其中的项 <c>IsEnabled=false</c> 置灰（自绘菜单会画禁用色且不挂点击）——让用户知道
    /// 这里确实有个插件，但它被声明为「滚轮找不到」，同样不可点。
    /// </para>
    /// <para>只管「跳转」，不表达当前状态——因此不打勾、不显示当前项。</para>
    /// </summary>
    public static IReadOnlyList<MenuContribution> BuildPanelChildren(
        MenuTarget target,
        IPluginRegistry registry,
        IReadOnlyList<string> switchableIds,
        Action<string> switchTo)
    {
        var switchable = new HashSet<string>(switchableIds, StringComparer.OrdinalIgnoreCase);

        return registry.Plugins
            .Select(plugin => (Plugin: plugin, Skin: plugin as IIslandSkin))
            .Where(x => x.Skin is not null)
            .Select(x => new MenuContribution
            {
                Id = $"panels.switch.{x.Skin!.Id}",
                Header = x.Plugin.DisplayName,
                Target = target,
                Section = MenuSection.HostFixed,
                IsEnabled = switchable.Contains(x.Skin!.Id),
                Command = () => switchTo(x.Skin!.Id),
            })
            .ToArray();
    }
}
