using System.Collections.Generic;
using EndfieldCharge.Contracts;
using EndfieldCharge.Services;

namespace EndfieldCharge.Host.Plugins.Demo;

/// <summary>
/// 音乐控制演示插件：向灵动岛右键菜单注入 3 个媒体条目（上一曲 / 下一曲 / 暂停）。
/// 仅用于证明 C 类「插件注入项」路径——条目不内建在应用里，命令为演示 no-op。
/// </summary>
public sealed class MusicDemoPlugin : IPlugin, IContextMenuContributor
{
    private string _dataDirectory = string.Empty;

    public string Id => "demo.music";

    public string DisplayName => "音乐控制演示";

    public bool CanUnload => true;

    /// <summary>Initialize 时由宿主分配的每插件存储目录。</summary>
    public string DataDirectory => _dataDirectory;

    public void Initialize(IPluginContext context)
    {
        _dataDirectory = context.DataDirectory;
    }

    public void Shutdown()
    {
    }

    public IEnumerable<MenuContribution> GetMenuContributions(MenuContext context)
    {
        if (context.Target != MenuTarget.Island)
            yield break;

        yield return new MenuContribution
        {
            Id = "demo.music.prev",
            Header = Localization.MediaPrev,
            Target = MenuTarget.Island,
            Section = MenuSection.Content,
            Priority = 0,
            Command = () => Logger.Info("MusicDemo: 上一曲（演示，无实际动作）"),
        };
        yield return new MenuContribution
        {
            Id = "demo.music.next",
            Header = Localization.MediaNext,
            Target = MenuTarget.Island,
            Section = MenuSection.Content,
            Priority = 1,
            Command = () => Logger.Info("MusicDemo: 下一曲（演示，无实际动作）"),
        };
        yield return new MenuContribution
        {
            Id = "demo.music.pause",
            Header = Localization.MediaPause,
            Target = MenuTarget.Island,
            Section = MenuSection.Content,
            Priority = 2,
            Command = () => Logger.Info("MusicDemo: 暂停（演示，无实际动作）"),
        };
    }
}
