namespace EndfieldCharge.Contracts;

/// <summary>岛内容色调。</summary>
public enum IslandTone
{
    Normal,
    Warning,
    Danger,
    Overflow,
}

/// <summary>播放方式：Full = 完整三态动画；Simple = 简化胶囊动画。</summary>
public enum IslandPlayKind
{
    Full,
    Simple,
}

/// <summary>
/// 灵动岛一帧内容的不可变描述（插件 → 宿主渲染层）。
/// 仅依赖 BCL。
/// </summary>
public sealed record IslandContentDescriptor
{
    /// <summary>大号居中标题。</summary>
    public string? Title { get; init; }

    /// <summary>小号 tagline。</summary>
    public string? TagLine { get; init; }

    /// <summary>数值文本，例如 "62.4"。</summary>
    public string? ValueText { get; init; }

    /// <summary>数值单位，例如 "/90.0" 或 "%"。</summary>
    public string? ValueUnit { get; init; }

    /// <summary>百分比文本，例如 "69"。</summary>
    public string? PercentText { get; init; }

    /// <summary>徽章圆弧进度 0..1。</summary>
    public double RingFraction { get; init; }

    public bool ShowRing { get; init; } = true;

    public IslandTone Tone { get; init; } = IslandTone.Normal;

    public IslandPlayKind PlayKind { get; init; } = IslandPlayKind.Simple;
}
