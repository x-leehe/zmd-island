using System;
using System.Threading;
using System.Threading.Tasks;
using EndfieldCharge.Services;

namespace EndfieldCharge.Host.Plugins.Music;

/// <summary>
/// 歌词调度：按设置选来源、按曲目去重、换曲时取消上一次请求、按播放位置取当前行。
/// <para>
/// 只做调度 —— 不碰 UI、不碰网络细节；provider 由外部注入，因此可单元测试。
/// 事件在调用方线程上触发（插件负责 marshal 到 UI 线程）。
/// </para>
/// </summary>
public sealed class LyricsService : IDisposable
{
    /// <summary>旧设置里的值：SMTC 本身不提供歌词 —— 迁到 <c>merge</c>（三源并行择优）。</summary>
    public const string LegacySource = "smtc";

    /// <summary>旧的「自动（顺序回退）」取值，现已并入 <c>merge</c>（那是并行的，不是串行的）。</summary>
    public const string LegacyAutoSource = "auto";

    private readonly Func<string, ILyricsProvider?> _providerFactory;

    private CancellationTokenSource? _cts;
    private ILyricsProvider? _provider;
    private string _source = string.Empty;
    private string _key = string.Empty;
    private int _attempts;
    private LrcTimeline _timeline = LrcTimeline.Empty;
    private bool _disposed;

    /// <summary>
    /// 同一首最多试几次。纯音乐 / 库里没有的曲子会被各源判为"无可信歌词"，
    /// 而 SMTC 的元数据与时间线事件会**反复**通知同一首 —— 不设上限就会变成每秒联网好几次。
    /// </summary>
    public const int MaxAttemptsPerTrack = 3;

    /// <param name="providerFactory">按来源名造 provider；返回 <c>null</c> 表示该来源不提供歌词（如「关闭」）。</param>
    public LyricsService(Func<string, ILyricsProvider?> providerFactory)
        => _providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));

    /// <summary>歌词内容有变化（拉到新歌词 / 换曲清空）—— 调用方据此刷新显示。</summary>
    public event Action? Changed;

    /// <summary>当前来源名（规范化后）。</summary>
    public string Source => _source;

    /// <summary>当前是否已有可用于显示的时间轴。</summary>
    public bool HasLyrics => !_timeline.IsEmpty;

    /// <summary>切换歌词来源（设置改动时调用）。来源变了会把已就绪的歌词丢掉，等下一次换曲重取。</summary>
    public void Use(string? source)
    {
        var normalized = Normalize(source);
        if (normalized == _source)
            return;

        _source = normalized;
        _provider = _providerFactory(normalized);

        _key = string.Empty;      // 同一首在换来源后也要重取
        _attempts = 0;
        _timeline = LrcTimeline.Empty;
    }

    /// <summary>
    /// 换曲 / 元数据变化时调用。同一首重复通知不会重复请求；
    /// 换曲会**立即取消**上一次未完成的请求。
    /// </summary>
    public async Task OnTrackChangedAsync(LyricsQuery? query)
    {
        // 同一首：
        //   * 正在取 → 什么都不做（SMTC 的元数据 / 时间线事件会反复通知同一首）；
        //   * 已经拿到歌词 → 什么都不做（否则会把已显示的抹掉，且不再重取）；
        //   * 取不到（纯音乐等）→ 最多再试 MaxAttemptsPerTrack 次，之后静默，换曲才重置。
        if (query is not null && query.IsUsable && query.Key == _key &&
            (_cts is not null || !_timeline.IsEmpty || _attempts >= MaxAttemptsPerTrack))
        {
            return;
        }

        // 先取消上一次：换曲时旧歌词立刻失效，也不让旧响应写回
        CancelPending();

        _timeline = LrcTimeline.Empty;

        if (query is null || !query.IsUsable || _provider is null)
        {
            _key = string.Empty;
            _attempts = 0;
            RaiseChanged();
            return;
        }

        if (query.Key != _key)
        {
            _key = query.Key;
            _attempts = 0;
        }

        _attempts++;

        var cts = new CancellationTokenSource();
        _cts = cts;
        try
        {
            var timeline = await _provider.FetchAsync(query, cts.Token).ConfigureAwait(false);
            if (cts.IsCancellationRequested || _disposed)
                return;

            _timeline = timeline ?? LrcTimeline.Empty;
            RaiseChanged();
        }
        catch (OperationCanceledException)
        {
            // 换曲打断：正常路径，不记日志
        }
        catch (Exception ex)
        {
            Logger.Warn($"Music: 取歌词失败 —— {ex.Message}");
        }
        finally
        {
            if (ReferenceEquals(_cts, cts))
                _cts = null;

            // 总是释放：被换曲打断的那次由它自己的 finally 负责收尾
            cts.Dispose();
        }
    }

    /// <summary>取该播放位置应显示的歌词（没有歌词时为空串）。</summary>
    public string CurrentAt(TimeSpan position) => _timeline.CurrentAt(position);

    /// <summary>该位置在**当前歌词行内**的进度（0..1）—— 供长句"跟唱"滚动使用。</summary>
    public double ProgressAt(TimeSpan position) => _timeline.ProgressAt(position);

    /// <inheritdoc />
    public void Dispose()
    {
        _disposed = true;
        CancelPending();
        _timeline = LrcTimeline.Empty;
        _provider = null;
    }

    private void CancelPending()
    {
        var cts = _cts;
        _cts = null;
        if (cts is null)
            return;

        try
        {
            cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // 已随上一次请求结束而释放
        }
    }

    private void RaiseChanged()
    {
        try
        {
            Changed?.Invoke();
        }
        catch (Exception ex)
        {
            Logger.Warn($"Music: 歌词刷新回调异常 —— {ex.Message}");
        }
    }

    private static string Normalize(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return "off";

        var value = source.Trim().ToLowerInvariant();
        return value is LegacySource or LegacyAutoSource ? "merge" : value;
    }
}
