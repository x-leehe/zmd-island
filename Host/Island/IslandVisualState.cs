namespace EndfieldCharge.Host.Island;

/// <summary>
/// 灵动岛的四种可见状态：
///   Hidden     —— 完全隐藏；
///   Response   —— 插拔电等事件触发的完整 / 简化响应动画；
///   Waiting    —— 560×60 胶囊（Wh / % / 徽章），空闲 T1 后收缩；
///   Contracted —— 收缩态（仅电量环 + 百分比），空闲 T2 后隐藏。
/// </summary>
public enum IslandVisualState
{
    Hidden,
    Response,
    Waiting,
    Contracted,
}
