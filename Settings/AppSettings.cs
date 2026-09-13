namespace EndfieldCharge.Settings;

public sealed record AppSettings
{
    public double GlobalScale { get; init; } = 0.8;

    /// <summary>等待态超时（秒）：状态 C 空闲多久后收缩为收缩态。</summary>
    public double WaitingTimeoutSeconds { get; init; } = 4.0;

    /// <summary>收缩态超时（秒）：收缩态空闲多久后隐藏。</summary>
    public double ContractedTimeoutSeconds { get; init; } = 4.0;

    /// <summary>回弹强度 0~0.5。映射到 KS_BackOut 第二控制点 Y = 1 + 值，越大过冲越明显。</summary>
    public double BounceStrength { get; init; } = 0.275;

    /// <summary>波纹强度倍率 0~2。乘到各圈波纹峰值透明度上。</summary>
    public double RippleIntensity { get; init; } = 1.0;

    /// <summary>波纹幅度倍率 0.5~1.5。乘到各圈波纹最终扩散 scale 上。</summary>
    public double RippleSpread { get; init; } = 1.0;

    public HudPosition HudPosition { get; init; } = HudPosition.TopCenter;

    /// HUD 所在显示器：-1 => 主显示器（默认）。
    public int MonitorIndex { get; init; } = -1;
    public string Language { get; init; } = "auto";
    public int LowBatteryThreshold { get; init; } = 20;
    public bool EnableLowBatteryAlert { get; init; } = true;
    public bool EnableFullChargeAlert { get; init; } = true;
    public bool EnablePowerSaverNotify { get; init; } = true;
    public bool EnableAutoStart { get; init; } = false;

    /// <summary>HUD 窗口置顶（灵动岛右键菜单「窗口置顶」可切换）。</summary>
    public bool WindowTopmost { get; init; } = true;

    /// <summary>鼠标滚轮在岛上的切换行为（循环 / 到边界停止 / 禁用）。</summary>
    public WheelSwitchMode WheelSwitch { get; init; } = WheelSwitchMode.Wrap;
}

/// <summary>滚轮在岛上切换岛的行为。</summary>
public enum WheelSwitchMode
{
    /// <summary>循环切换：最后一个之后再滚回到第一个。</summary>
    Wrap,

    /// <summary>到边界即停：已在第一个 / 最后一个时不再切换。</summary>
    Clamp,

    /// <summary>禁用滚轮切换岛。</summary>
    Disabled,
}

public enum HudPosition
{
    TopCenter,
    TopRight,
    TopLeft,
}
