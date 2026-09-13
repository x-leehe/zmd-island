namespace EndfieldCharge.Contracts;

/// <summary>菜单目标：灵动岛右键菜单 / 托盘菜单。</summary>
public enum MenuTarget
{
    Island,
    Tray,
}

/// <summary>菜单段：插件内容区 / 宿主固定区。</summary>
public enum MenuSection
{
    Content,
    HostFixed,
}

/// <summary>
/// 一条菜单贡献（插件 → 宿主菜单合成器）。仅依赖 BCL。
/// </summary>
public sealed record MenuContribution
{
    public required string Id { get; init; }

    public required string Header { get; init; }

    public MenuTarget Target { get; init; } = MenuTarget.Island;

    public MenuSection Section { get; init; } = MenuSection.Content;

    public int Priority { get; init; }

    public bool IsVisible { get; init; } = true;

    public bool IsEnabled { get; init; } = true;

    public bool IsChecked { get; init; }

    public bool IsSeparator { get; init; }

    public string? IconKey { get; init; }

    public Action? Command { get; init; }

    /// <summary>子菜单（如「插件 ▸」下的受管插件列表）。</summary>
    public IReadOnlyList<MenuContribution>? Children { get; init; }
}

/// <summary>菜单合成上下文（目标菜单）。仅依赖 BCL。</summary>
public sealed record MenuContext
{
    public required MenuTarget Target { get; init; }
}

/// <summary>上下文菜单贡献者：插件向指定目标菜单注入条目。仅依赖 BCL。</summary>
public interface IContextMenuContributor
{
    IEnumerable<MenuContribution> GetMenuContributions(MenuContext context);
}
