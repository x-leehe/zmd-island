namespace EndfieldCharge.Contracts;

/// <summary>
/// 宿主提供给插件的最小上下文：每插件存储目录 + 服务定位。
/// 仅依赖 BCL。
/// </summary>
public interface IPluginContext
{
    /// <summary>该插件专属的持久化目录（宿主负责创建）。</summary>
    string DataDirectory { get; }

    /// <summary>获取宿主提供的服务；无对应服务时返回 null。</summary>
    T? GetService<T>() where T : class;
}
