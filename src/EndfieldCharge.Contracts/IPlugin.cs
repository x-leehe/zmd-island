namespace EndfieldCharge.Contracts;

/// <summary>
/// 插件契约：宿主加载 / 卸载的最小接口。
/// 该程序集仅依赖 BCL——未来可拆分为独立、无 UI 依赖的共享程序集。
/// </summary>
public interface IPlugin
{
    /// <summary>稳定标识，例如 "battery.meta"。</summary>
    string Id { get; }

    /// <summary>用户可见名称。</summary>
    string DisplayName { get; }

    /// <summary>false 表示不可卸载的元插件（如电池元数据源）。</summary>
    bool CanUnload { get; }

    void Initialize(IPluginContext context);

    void Shutdown();
}
