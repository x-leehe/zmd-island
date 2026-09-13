using System;
using EndfieldCharge.Services;

namespace EndfieldCharge.Host.Island;

/// <summary>
/// 灵动岛四态状态机（纯逻辑、可测试，不依赖 Avalonia）：
///   Hidden     —— 完全隐藏；仅通过悬停驻留或显式 ShowWaiting/Response 离开。
///   Response   —— 响应动画（插拔电）；结束后进入 Waiting。
///   Waiting    —— 完整胶囊（Wh / % / 徽章）；空闲 T1 后收缩为 Contracted。
///   Contracted —— 收缩胶囊（仅电量环 + 百分比）；空闲 T2 后隐藏。
///
/// 合法转换表（其余一律拒绝，自转换返回 false）：
///   Hidden     → Response / Waiting / Contracted
///   Response   → Hidden / Waiting
///   Waiting    → Hidden / Contracted / Response
///   Contracted → Waiting / Response / Hidden
/// </summary>
public class IslandStateMachine
{
    public IslandVisualState Current { get; private set; } = IslandVisualState.Hidden;

    /// <summary>状态变更事件：参数为 (前一状态, 新状态)。</summary>
    public event Action<IslandVisualState, IslandVisualState>? StateChanged;

    /// <summary>
    /// 按合法转换表尝试转换。非法（含自转换）时记录警告并返回 false，
    /// 不改动 Current、不触发事件。
    /// </summary>
    public bool TryTransition(IslandVisualState next)
    {
        if (next == Current)
            return false;

        if (!IsLegal(Current, next))
        {
            Logger.Warn($"IslandStateMachine: 非法状态转换 {Current} → {next} 被拒绝");
            return false;
        }

        var previous = Current;
        Current = next;
        StateChanged?.Invoke(previous, next);
        return true;
    }

    /// <summary>
    /// 无条件转换（显式显示 / 隐藏用），绕过合法转换表，
    /// 始终设置 Current 并触发事件（含自转换）。
    /// </summary>
    public void Force(IslandVisualState next)
    {
        var previous = Current;
        Current = next;
        StateChanged?.Invoke(previous, next);
    }

    private static bool IsLegal(IslandVisualState from, IslandVisualState to) => (from, to) switch
    {
        (IslandVisualState.Hidden, IslandVisualState.Response) => true,
        (IslandVisualState.Hidden, IslandVisualState.Waiting) => true,
        (IslandVisualState.Hidden, IslandVisualState.Contracted) => true,
        (IslandVisualState.Response, IslandVisualState.Hidden) => true,
        (IslandVisualState.Response, IslandVisualState.Waiting) => true,
        (IslandVisualState.Waiting, IslandVisualState.Hidden) => true,
        (IslandVisualState.Waiting, IslandVisualState.Contracted) => true,
        (IslandVisualState.Waiting, IslandVisualState.Response) => true,
        (IslandVisualState.Contracted, IslandVisualState.Waiting) => true,
        (IslandVisualState.Contracted, IslandVisualState.Response) => true,
        (IslandVisualState.Contracted, IslandVisualState.Hidden) => true,
        _ => false,
    };
}
