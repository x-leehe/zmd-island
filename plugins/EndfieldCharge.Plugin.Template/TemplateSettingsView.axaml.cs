using System;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;

namespace EndfieldCharge.Host.Plugins.Template;

/// <summary>
/// 模板插件设置面板：演示宿主全局设置样式与四种控件的「改动即保存」模式。
/// 面板只负责回写完整设置；持久化由插件负责（<see cref="TemplateSettingsStore"/>）。
/// </summary>
public partial class TemplateSettingsView : UserControl
{
    private readonly Func<TemplateSettings> _read;
    private readonly Action<TemplateSettings> _write;

    /// <summary>滑块 ↔ 步进器同步中：阻止互相回灌。</summary>
    private bool _syncing;

    /// <summary>构造期控件赋值会触发 Changed 事件：加载完成前不允许保存。</summary>
    private bool _loaded;

    /// <param name="read">读取插件当前设置。</param>
    /// <param name="write">写回设置（插件据此持久化）。</param>
    public TemplateSettingsView(Func<TemplateSettings> read, Action<TemplateSettings> write)
    {
        InitializeComponent();

        _read = read;
        _write = write;

        ApplyLocalization();

        var settings = read();
        FlagSwitch.IsChecked = settings.Flag;
        NumberSlider.Value = settings.Number;
        SelectChoice(settings.Choice);
        TextValue.Text = settings.Text;

        WirePair(NumberSlider, NumberStepper, "{0:F1}");

        FlagSwitch.IsCheckedChanged += (_, _) => Save();
        ChoiceCombo.SelectionChanged += (_, _) => Save();
        TextValue.TextChanged += (_, _) => Save();

        _loaded = true;
    }

    private void ApplyLocalization()
    {
        SectionText.Text = Localization.TemplateSectionTitle;
        LabelFlag.Text = Localization.TemplateLabelFlag;
        DescFlag.Text = Localization.TemplateDescFlag;
        LabelNumber.Text = Localization.TemplateLabelNumber;
        DescNumber.Text = Localization.TemplateDescNumber;
        LabelChoice.Text = Localization.TemplateLabelChoice;
        DescChoice.Text = Localization.TemplateDescChoice;
        LabelText.Text = Localization.TemplateLabelText;
        DescText.Text = Localization.TemplateDescText;

        TextValue.Watermark = Localization.TemplateTextPlaceholder;

        ChoiceCombo.Items.Clear();
        ChoiceCombo.Items.Add(new ComboBoxItem { Content = Localization.TemplateChoiceAlpha, Tag = "alpha" });
        ChoiceCombo.Items.Add(new ComboBoxItem { Content = Localization.TemplateChoiceBeta, Tag = "beta" });
        ChoiceCombo.Items.Add(new ComboBoxItem { Content = Localization.TemplateChoiceGamma, Tag = "gamma" });
    }

    /// <summary>把设置里的选项选中（**按 Tag 匹配，不按序号** —— 下拉顺序会变）。</summary>
    private void SelectChoice(string choice)
    {
        foreach (var item in ChoiceCombo.Items)
        {
            if (item is ComboBoxItem { Tag: string tag } &&
                string.Equals(tag, choice, StringComparison.OrdinalIgnoreCase))
            {
                ChoiceCombo.SelectedItem = item;
                return;
            }
        }

        ChoiceCombo.SelectedIndex = 0;
    }

    private string SelectedChoice() =>
        ChoiceCombo.SelectedItem is ComboBoxItem { Tag: string tag } ? tag : "alpha";

    /// <summary>滑块 + 步进器双向绑定：两者编辑同一个值，任一改动即时保存。</summary>
    private void WirePair(Slider slider, NumericUpDown stepper, string format)
    {
        stepper.FormatString = format;

        _syncing = true;
        stepper.Value = (decimal)slider.Value;
        _syncing = false;

        slider.PropertyChanged += (_, e) =>
        {
            if (e.Property != RangeBase.ValueProperty || _syncing)
                return;

            _syncing = true;
            stepper.Value = (decimal)slider.Value;
            _syncing = false;
            Save();
        };

        stepper.PropertyChanged += (_, e) =>
        {
            if (e.Property != NumericUpDown.ValueProperty || _syncing || stepper.Value is not decimal value)
                return;

            _syncing = true;
            slider.Value = Math.Clamp((double)value, slider.Minimum, slider.Maximum);
            _syncing = false;
            Save();
        };
    }

    /// <summary>把四个控件的当前值写回设置（插件负责持久化），并给出「已保存」提示。</summary>
    private void Save()
    {
        if (!_loaded)
            return;

        _write(_read() with
        {
            Flag = FlagSwitch.IsChecked == true,
            Number = Math.Round(NumberSlider.Value, 1),
            Choice = SelectedChoice(),
            Text = TextValue.Text ?? string.Empty,
        });

        SavedText.Text = Localization.SavedToast;
        SavedText.Opacity = 1;
        DispatcherTimer.RunOnce(() => SavedText.Opacity = 0, TimeSpan.FromSeconds(2));
    }
}
