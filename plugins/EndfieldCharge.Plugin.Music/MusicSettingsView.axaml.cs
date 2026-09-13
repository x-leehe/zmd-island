using System;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;

namespace EndfieldCharge.Host.Plugins.Music;

/// <summary>
/// 音乐插件设置面板：改动即保存并回流到插件（插件负责持久化与生效，宿主不参与语义）。
/// </summary>
public partial class MusicSettingsView : UserControl
{
    private readonly Func<MusicSettings> _read;
    private readonly Action<MusicSettings> _write;

    /// <param name="read">读取插件当前设置。</param>
    /// <param name="write">写回设置（插件据此持久化并立即生效）。</param>
    public MusicSettingsView(Func<MusicSettings> read, Action<MusicSettings> write)
    {
        InitializeComponent();

        _read = read;
        _write = write;

        ApplyLocalization();

        var s = read();
        ExpandedTimeoutSlider.Value = s.ExpandedTimeoutSeconds;
        ShowTitleSwitch.IsChecked = s.ShowTitleWhenNoLyric;
        VisualizerSwitch.IsChecked = s.ShowVisualizer;
        LyricSourceCombo.SelectedIndex = s.LyricSource switch
        {
            "local" => 1,
            "lrclib" => 2,
            _ => 0,
        };
        UpdateExpandedText();

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
    }

    private void ApplyLocalization()
    {
        SectionText.Text = Localization.MusicSectionTitle;
        LabelExpandedTimeout.Text = Localization.LabelExpandedTimeout;
        LabelShowTitle.Text = Localization.LabelShowTitleWhenNoLyric;
        LabelVisualizer.Text = Localization.LabelShowVisualizer;
        LabelLyricSource.Text = Localization.LabelLyricSource;

        LyricSourceCombo.Items.Clear();
        LyricSourceCombo.Items.Add(new ComboBoxItem { Content = Localization.LyricSourceSmtc, Tag = "smtc" });
        LyricSourceCombo.Items.Add(new ComboBoxItem { Content = Localization.LyricSourceLocal, Tag = "local" });
        LyricSourceCombo.Items.Add(new ComboBoxItem { Content = Localization.LyricSourceLrclib, Tag = "lrclib" });
    }

    private void UpdateExpandedText() => ExpandedTimeoutValue.Text = $"{ExpandedTimeoutSlider.Value:F1}s";

    private void Save()
    {
        _write(_read() with
        {
            ExpandedTimeoutSeconds = Math.Round(ExpandedTimeoutSlider.Value, 1),
            ShowTitleWhenNoLyric = ShowTitleSwitch.IsChecked == true,
            ShowVisualizer = VisualizerSwitch.IsChecked == true,
            LyricSource = LyricSourceCombo.SelectedIndex switch
            {
                1 => "local",
                2 => "lrclib",
                _ => "smtc",
            },
        });

        SavedText.Text = Localization.SavedToast;
        SavedText.Opacity = 1;
        DispatcherTimer.RunOnce(() => SavedText.Opacity = 0, TimeSpan.FromSeconds(2));
    }
}
