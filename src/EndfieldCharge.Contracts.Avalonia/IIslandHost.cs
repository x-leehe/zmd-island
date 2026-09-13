using System.Collections.Generic;

namespace EndfieldCharge.Contracts.Avalonia;

/// <summary>
/// 宿主提供的岛服务：插件经
/// <see cref="EndfieldCharge.Contracts.IPluginContext.GetService{T}"/> 取得，用于
/// 查询有哪些岛皮肤、当前是哪个，以及程序化切换（滚轮切换的插件侧入口）。
/// </summary>
public interface IIslandHost
{
    /// <summary>当前岛皮肤 Id。</summary>
    string CurrentSkinId { get; }

    /// <summary>参与滚轮轮转的岛皮肤 Id（顺序 = 轮转顺序，电池在首位）。</summary>
    IReadOnlyList<string> WheelSkinIds { get; }

    /// <summary>切换到指定 Id 的岛皮肤；不存在或已是当前皮肤时返回 false。</summary>
    bool SwitchSkin(string id);

    /// <summary>
    /// 把岛切到指定皮肤并<strong>以展开态（宿主 Response 状态）主动弹出一次</strong>，
    /// 用于「内容开始播放」这类主动提示（如音乐开始播放）。不存在该皮肤返回 false。
    /// <para>仅面向自给数据的皮肤；宿主内容皮肤（如电池）请走既有入口。</para>
    /// </summary>
    bool ShowExpanded(string id);
}
