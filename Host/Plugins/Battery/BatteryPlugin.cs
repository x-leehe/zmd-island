using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using EndfieldCharge.Animations;
using EndfieldCharge.Contracts;
using EndfieldCharge.Host.Island;
using EndfieldCharge.Services;

namespace EndfieldCharge.Host.Plugins.Battery;

/// <summary>
/// 电池元插件：同时实现
///   · IPlugin               —— 插件生命周期（不可卸载的元插件）；
///   · IIslandContentProvider—— 把 BatterySnapshot 映射为 IslandContentDescriptor；
///   · IIslandSkin           —— 岛皮肤（委托给 BatteryIslandView + BatteryAnimationTheme）。
/// Phase 2 由 HudWindow 直接实例化（插件注册表 Phase 3 引入）。
/// </summary>
public sealed class BatteryPlugin : IPlugin, IIslandContentProvider, IIslandSkin
{
    private readonly BatteryIslandView _view = new();
    private IslandContentDescriptor? _lastContent;
    private string _dataDirectory = string.Empty;

    // ---------------- IPlugin ----------------

    public string Id => "battery.meta";

    public string DisplayName => "电池充电检测";

    public bool CanUnload => false;

    /// <summary>Initialize 时由宿主分配的每插件存储目录。</summary>
    public string DataDirectory => _dataDirectory;

    public void Initialize(IPluginContext context)
    {
        _dataDirectory = context.DataDirectory;
    }

    public void Shutdown()
    {
    }

    // ---------------- IIslandContentProvider ----------------

    /// <summary>最近一帧内容；无内容时返回 null。</summary>
    public IslandContentDescriptor? GetContent() => _lastContent;

    /// <summary>异步取当前电池快照（宿主 ShowWaitingAsync 等入口用）。</summary>
    public Task<BatterySnapshot?> FetchSnapshotAsync() => Task.Run(BatteryService.GetSnapshot);

    /// <summary>
    /// BatterySnapshot → IslandContentDescriptor 映射：
    ///   · Title / TagLine —— 充电（超充）或省电模式文案；Simple 播放不设置（保持原样）；
    ///   · ValueText / ValueUnit / PercentText —— Wh 与百分比（无电池时为 "--"）；
    ///   · RingFraction = Percent/100，ShowRing = HasBattery，Tone = <20% ? Danger : Normal；
    ///   · PlayKind —— Full（完整三态）/ Simple（简化胶囊）。
    /// </summary>
    public IslandContentDescriptor CreateDescriptor(
        BatterySnapshot? snap,
        bool powerSaver,
        IslandPlayKind playKind)
    {
        string? title = null;
        string? tagLine = null;
        if (playKind == IslandPlayKind.Full)
        {
            title = powerSaver ? Localization.TitleSaver : Localization.TitleMode;
            tagLine = powerSaver ? Localization.TagLineSaver : Localization.TagLine;
        }

        if (snap is null || !snap.HasBattery)
        {
            return new IslandContentDescriptor
            {
                Title = title,
                TagLine = tagLine,
                ValueText = "--",
                ValueUnit = string.Empty,
                PercentText = "--",
                RingFraction = 0d,
                ShowRing = false,
                Tone = IslandTone.Normal,
                PlayKind = playKind,
            };
        }

        return new IslandContentDescriptor
        {
            Title = title,
            TagLine = tagLine,
            ValueText = (snap.RemainingWh * 1000).ToString("F0"),
            ValueUnit = $"/{snap.FullWh * 1000:F0}",
            PercentText = snap.Percent.ToString(),
            RingFraction = snap.Percent / 100d,
            ShowRing = true,
            Tone = snap.Percent < 20 ? IslandTone.Danger : IslandTone.Normal,
            PlayKind = playKind,
        };
    }

    // ---------------- IIslandSkin ----------------

    public Control View => _view;

    public void BindContent(IslandContentDescriptor content)
    {
        _lastContent = content;
        _view.BindContent(content);
    }

    public Task PlayResponseAsync(CancellationToken ct) => _view.PlayResponseAsync(ct);

    public Task PlayWaitingAsync(CancellationToken ct) => _view.PlayWaitingAsync(ct);

    public Task PlayContractAsync(CancellationToken ct) => _view.PlayContractAsync(ct);

    public Task PlayDismissAsync(CancellationToken ct) => _view.PlayDismissAsync(ct);

    public void ApplyScale(double globalScale) => _view.ApplyScale(globalScale);

    /// <summary>胶囊当前宽度（DIP）：宿主点击穿透 / 悬停命中区随收缩态收窄。</summary>
    public double PillWidthDips => _view.PillWidthDips;

    /// <summary>指针进入 / 离开胶囊（宿主据此暂停 / 重置空闲计时）。</summary>
    public event Action? IslandPointerEntered
    {
        add => _view.IslandPointerEntered += value;
        remove => _view.IslandPointerEntered -= value;
    }

    public event Action? IslandPointerExited
    {
        add => _view.IslandPointerExited += value;
        remove => _view.IslandPointerExited -= value;
    }

    // ---------------- 宿主便捷方法（注册表 Phase 3 之前直接调用） ----------------

    public void SetAnimationOptions(AnimationOptions options) => _view.SetAnimationOptions(options);

    public void RefreshLocalization() => _view.ApplyLocalization();

    /// <summary>调试用：--debug-ring 静态状态 C 画面。</summary>
    public void ShowStatic() => _view.ShowStatic();

    /// <summary>布置揭示起点（宿主在 Show 窗口前调用，避免首帧闪现整只胶囊）。</summary>
    public void PrepareRevealStart() => _view.PrepareRevealStart();
}
