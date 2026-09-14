using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using EndfieldCharge.Contracts;
using EndfieldCharge.Contracts.Avalonia;
using EndfieldCharge.Host.Island;
using EndfieldCharge.Services;

namespace EndfieldCharge.Host.Plugins.Template;

/// <summary>
/// 模板插件（外部 DLL，自带一切；第三方插件的起点）：
///   · IPlugin           —— 生命周期（Initialize 读取数据目录，Shutdown 断开接线）；
///   · IIslandSkin       —— 三态空壳皮肤（只画胶囊背景，见 <see cref="TemplateIslandView"/>）；
///   · IPluginSettingsPage —— 设置页示例（开关 / 数值 / 选项 / 文本，改动即存到插件数据目录）。
/// 宿主只按契约认识它，不引用本类型。
/// </summary>
public sealed class TemplatePlugin : IPlugin, IIslandSkin, IPluginSettingsPage
{
    private readonly TemplateIslandView _view = new();
    private string _dataDirectory = string.Empty;
    private TemplateSettings _settings = new();

    // ---------------- IPlugin ----------------

    public string Id => "template";

    public string DisplayName => Localization.PluginTemplateName;

    public bool CanUnload => true;

    public string DataDirectory => _dataDirectory;

    public void Initialize(IPluginContext context)
    {
        _dataDirectory = context.DataDirectory;
        _settings = TemplateSettingsStore.Load(_dataDirectory);
        Logger.Info($"Template: 设置已加载（开关 {_settings.Flag} / 数值 {_settings.Number:F1} / 选项 {_settings.Choice}）");
    }

    /// <summary>卸载：模板没有定时器 / 非托管资源，这里只记录生命周期（真实插件在此退订 / 释放）。</summary>
    public void Shutdown() => Logger.Info("Template: 已卸载");

    // ---------------- IIslandSkin ----------------

    public Control View => _view;

    /// <summary>模板皮肤自带空壳，不消费宿主内容（电池元插件的内容描述符只会被忽略）。</summary>
    public void BindContent(IslandContentDescriptor content)
    {
    }

    public Task PlayResponseAsync(CancellationToken ct) => _view.PlayResponseAsync(ct);

    public Task PlayWaitingAsync(CancellationToken ct) => _view.PlayWaitingAsync(ct);

    public Task PlayContractAsync(CancellationToken ct) => _view.PlayContractAsync(ct);

    public Task PlayDismissAsync(CancellationToken ct) => _view.PlayDismissAsync(ct);

    public void ApplyScale(double globalScale) => _view.ApplyScale(globalScale);

    public IslandMetrics CurrentMetrics => _view.CurrentMetrics;

    public IslandMetrics HoverMetrics => _view.HoverMetrics;

    public void PrepareRevealStart() => _view.PrepareRevealStart();

    // ---------------- 皮肤特性（覆写 IIslandSkin 的默认接口成员） ----------------

    /// <summary>数据自给（空壳），不使用宿主电池内容。</summary>
    public bool UsesHostContent => false;

    /// <summary>承载 560×160 展开态所需的最小窗口高度。</summary>
    public double WindowHeight => 160d;

    // ---------------- IPluginSettingsPage ----------------

    public string SettingsTitle => DisplayName;

    public Control CreateSettingsView() => new TemplateSettingsView(() => _settings, UpdateSettings);

    /// <summary>写回设置：持久化到插件数据目录（宿主不参与插件设置的语义）。</summary>
    private void UpdateSettings(TemplateSettings settings)
    {
        _settings = settings;
        TemplateSettingsStore.Save(_dataDirectory, settings);
    }
}
