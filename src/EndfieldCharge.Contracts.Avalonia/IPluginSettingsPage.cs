using Avalonia.Controls;

namespace EndfieldCharge.Contracts.Avalonia;

/// <summary>
/// 可选插件能力：向设置窗口的「插件」页贡献一个设置面板（面板由插件自绘，
/// 宿主不需要理解其语义）。插件自己负责持久化——通常写到
/// <see cref="EndfieldCharge.Contracts.IPluginContext.DataDirectory"/>。
/// </summary>
public interface IPluginSettingsPage
{
    /// <summary>「插件」页里的条目名（如「音乐」）。</summary>
    string SettingsTitle { get; }

    /// <summary>创建设置面板视图（宿主每次打开设置窗口调用一次）。</summary>
    Control CreateSettingsView();
}
