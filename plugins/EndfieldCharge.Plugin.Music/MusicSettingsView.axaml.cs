using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace EndfieldCharge.Host.Plugins.Music;

/// <summary>
/// 音乐插件设置面板：改动即保存并回流到插件（插件负责持久化与生效，宿主不参与语义）。
/// </summary>
public partial class MusicSettingsView : UserControl
{
    private readonly Func<MusicSettings> _read;
    private readonly Action<MusicSettings> _write;
    private readonly Func<IReadOnlyList<string>> _knownSources;

    /// <summary>「见过的来源」的轻量刷新器：播放器一出现就能立刻在这里看到。</summary>
    private readonly DispatcherTimer _knownRefresh;

    /// <summary>上一次渲染的「见过的来源」签名（没变就不重建行，免得打断悬停 / 点击）。</summary>
    private string _knownSignature = string.Empty;

    /// <param name="read">读取插件当前设置。</param>
    /// <param name="write">写回设置（插件据此持久化并立即生效）。</param>
    /// <param name="knownSources">插件见过的来源（AUMID），用于列出可勾选的白名单候选。</param>
    public MusicSettingsView(
        Func<MusicSettings> read,
        Action<MusicSettings> write,
        Func<IReadOnlyList<string>> knownSources)
    {
        InitializeComponent();

        _read = read;
        _write = write;
        _knownSources = knownSources;

        ApplyLocalization();

        var s = read();
        ExpandedTimeoutSlider.Value = s.ExpandedTimeoutSeconds;
        ShowTitleSwitch.IsChecked = s.ShowTitleWhenNoLyric;
        VisualizerSwitch.IsChecked = s.ShowVisualizer;
        SelectLyricSource(s.LyricSource);
        UpdateExpandedText();

        WhitelistInput.Watermark = Localization.WhitelistPlaceholder;
        WhitelistAddButton.Content = Localization.WhitelistAdd;
        RenderWhitelist();   // 必须在这里渲染：上一版漏了这一步，面板里那张表永远是空的

        // 「见过的来源」每秒轻量刷新：播放器一出现立刻能看到、点一下即加入。
        // 上一版只在构造时算一次 —— 设置窗口开着的时候新播放器永远不出现，得关掉重开。
        _knownRefresh = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _knownRefresh.Tick += (_, _) => RenderKnown();
        _knownRefresh.Start();
        AttachedToVisualTree += (_, _) => _knownRefresh.Start();
        DetachedFromVisualTree += (_, _) => _knownRefresh.Stop();

        ExpandedTimeoutSlider.PropertyChanged += (_, e) =>
        {
            if (e.Property == RangeBase.ValueProperty)
            {
                UpdateExpandedText();
                Save();
            }
        };
        ShowTitleSwitch.IsCheckedChanged += (_, _) => Save();
        VisualizerSwitch.IsCheckedChanged += (_, _) => Save();
        LyricSourceCombo.SelectionChanged += (_, _) => Save();

        WhitelistAddButton.Click += (_, _) => AddSource(WhitelistInput.Text);
        WhitelistInput.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter)
                return;

            AddSource(WhitelistInput.Text);
            e.Handled = true;
        };
    }

    /// <summary>把设置里的来源名选中（**按 Tag 匹配，不按序号** —— 下拉顺序会变）。</summary>
    private void SelectLyricSource(string source)
    {
        foreach (var item in LyricSourceCombo.Items)
        {
            if (item is ComboBoxItem { Tag: string tag } &&
                string.Equals(tag, source, StringComparison.OrdinalIgnoreCase))
            {
                LyricSourceCombo.SelectedItem = item;
                return;
            }
        }

        LyricSourceCombo.SelectedIndex = 0;
    }

    private string SelectedLyricSource() =>
        LyricSourceCombo.SelectedItem is ComboBoxItem { Tag: string tag } ? tag : "merge";

    /// <summary>当前名单（设置是唯一真相，不依赖任何控件的选中态）。</summary>
    private List<string> CurrentSources() => new(_read().SpectrumSources ?? Array.Empty<string>());

    /// <summary>
    /// 重画「已允许」和「见过的来源」两张表。
    /// <para>
    /// 上一版把 ListBox 的 <c>IsSelected</c> 当状态源，于是有两个毛病：构造时忘了填（表一直是空的），
    /// 以及"控件选中态"与"设置里的名单"两处状态互相打架。现在一律从设置渲染、改动即回写。
    /// </para>
    /// </summary>
    private void RenderWhitelist()
    {
        RenderAllowed();
        _knownSignature = string.Empty;   // 名单变了 → 强制重画「见过的来源」
        RenderKnown();
    }

    private void RenderAllowed()
    {
        var allowed = CurrentSources();

        WhitelistRows.Children.Clear();
        foreach (var id in allowed)
            WhitelistRows.Children.Add(BuildAllowedRow(id));

        WhitelistEmptyLabel.IsVisible = allowed.Count == 0;
    }

    /// <summary>
    /// 「见过的来源」：由每秒的轻量 tick 调用，**只在集合真的变了时才重建行**
    /// （否则每秒重建会把鼠标悬停 / 点击打断）。
    /// </summary>
    private void RenderKnown()
    {
        var allowed = CurrentSources();
        var known = _knownSources()
            .Where(id => !string.IsNullOrWhiteSpace(id) && !allowed.Contains(id, StringComparer.OrdinalIgnoreCase))
            .ToList();

        var signature = string.Join('\u001f', known);
        if (signature == _knownSignature)
            return;

        _knownSignature = signature;

        WhitelistKnownRows.Children.Clear();
        foreach (var id in known)
            WhitelistKnownRows.Children.Add(BuildKnownRow(id));

        WhitelistKnownEmptyLabel.IsVisible = known.Count == 0;
    }

    /// <summary>改动名单：写回设置（连同其它控件当前值）后重画。</summary>
    private void SetSources(IEnumerable<string> sources)
    {
        var list = new List<string>();
        foreach (var id in sources)
        {
            var value = (id ?? string.Empty).Trim();
            if (value.Length > 0 && !list.Contains(value, StringComparer.OrdinalIgnoreCase))
                list.Add(value);
        }

        Save(list);
        RenderWhitelist();
    }

    private void AddSource(string? id)
    {
        var value = (id ?? string.Empty).Trim();
        if (value.Length == 0)
            return;

        var list = CurrentSources();
        if (!list.Contains(value, StringComparer.OrdinalIgnoreCase))
            list.Add(value);

        WhitelistInput.Text = string.Empty;
        SetSources(list);
    }

    private void RemoveSource(string id)
    {
        var list = CurrentSources();
        list.RemoveAll(item => string.Equals(item, id, StringComparison.OrdinalIgnoreCase));
        SetSources(list);
    }

    /// <summary>一行「已允许」：来源名 + ✕ 移除。</summary>
    private Control BuildAllowedRow(string id)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

        grid.Children.Add(new TextBlock
        {
            Text = id,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
        });

        var remove = new Button
        {
            Content = "✕",
            FontSize = 11,
            Padding = new Thickness(6, 0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = new SolidColorBrush(Color.Parse("#9A9A9A")),
        };
        remove.Click += (_, _) => RemoveSource(id);
        Grid.SetColumn(remove, 1);
        grid.Children.Add(remove);

        return Row(grid, "#2E2E30");
    }

    /// <summary>一行「见过的来源」：点一下即加入名单（省得手打 AUMID）。</summary>
    private Control BuildKnownRow(string id)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

        grid.Children.Add(new TextBlock
        {
            Text = id,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.Parse("#B8B8B8")),
        });

        var plus = new TextBlock
        {
            Text = "＋",
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.Parse("#C6CA4C")),
        };
        Grid.SetColumn(plus, 1);
        grid.Children.Add(plus);

        var row = Row(grid, "#262628");
        row.Cursor = new Cursor(StandardCursorType.Hand);
        row.PointerPressed += (_, e) =>
        {
            AddSource(id);
            e.Handled = true;
        };

        return row;
    }

    private static Border Row(Control inner, string background) => new()
    {
        Background = new SolidColorBrush(Color.Parse(background)),
        CornerRadius = new CornerRadius(6),
        Padding = new Thickness(8, 4),
        Child = inner,
    };

    private void ApplyLocalization()
    {
        SectionText.Text = Localization.MusicSectionTitle;
        LabelExpandedTimeout.Text = Localization.LabelExpandedTimeout;
        LabelShowTitle.Text = Localization.LabelShowTitleWhenNoLyric;
        LabelVisualizer.Text = Localization.LabelShowVisualizer;
        LabelLyricSource.Text = Localization.LabelLyricSource;
        LabelMusicSourceWhitelist.Text = Localization.LabelMusicSourceWhitelist;
        HintMusicSourceWhitelist.Text = Localization.HintMusicSourceWhitelist;
        WhitelistAllowedLabel.Text = Localization.WhitelistAllowed;
        WhitelistEmptyLabel.Text = Localization.WhitelistEmpty;
        WhitelistKnownLabel.Text = Localization.WhitelistKnown;
        WhitelistKnownEmptyLabel.Text = Localization.WhitelistKnownEmpty;

        LyricSourceCombo.Items.Clear();
        LyricSourceCombo.Items.Add(new ComboBoxItem { Content = Localization.LyricSourceMerge, Tag = "merge" });
        LyricSourceCombo.Items.Add(new ComboBoxItem { Content = Localization.LyricSourcePreferLrclib, Tag = "prefer-lrclib" });
        LyricSourceCombo.Items.Add(new ComboBoxItem { Content = Localization.LyricSourcePreferNetease, Tag = "prefer-netease" });
        LyricSourceCombo.Items.Add(new ComboBoxItem { Content = Localization.LyricSourcePreferLocal, Tag = "prefer-local" });
        LyricSourceCombo.Items.Add(new ComboBoxItem { Content = Localization.LyricSourceLrclib, Tag = "lrclib" });
        LyricSourceCombo.Items.Add(new ComboBoxItem { Content = Localization.LyricSourceNetease, Tag = "netease" });
        LyricSourceCombo.Items.Add(new ComboBoxItem { Content = Localization.LyricSourceLocal, Tag = "local" });
        LyricSourceCombo.Items.Add(new ComboBoxItem { Content = Localization.LyricSourceOff, Tag = "off" });
    }

    private void UpdateExpandedText() => ExpandedTimeoutValue.Text = $"{ExpandedTimeoutSlider.Value:F1}s";

    /// <param name="sourcesOverride">白名单改动走这里；为空表示"沿用当前名单"。</param>
    private void Save(IReadOnlyList<string>? sourcesOverride = null)
    {
        _write(_read() with
        {
            ExpandedTimeoutSeconds = Math.Round(ExpandedTimeoutSlider.Value, 1),
            ShowTitleWhenNoLyric = ShowTitleSwitch.IsChecked == true,
            ShowVisualizer = VisualizerSwitch.IsChecked == true,
            LyricSource = SelectedLyricSource(),
            // 空数组是合法状态：不采集频谱、也不主动弹岛
            SpectrumSources = sourcesOverride ?? CurrentSources(),
        });

        SavedText.Text = Localization.SavedToast;
        SavedText.Opacity = 1;
        DispatcherTimer.RunOnce(() => SavedText.Opacity = 0, TimeSpan.FromSeconds(2));
    }
}
