using System.Speech.Synthesis;
using System.Windows;
using ClassroomToolkit.App.Settings;
using ClassroomToolkit.Interop.Presentation;
using System.Linq;
using ClassroomToolkit.App.Helpers;

namespace ClassroomToolkit.App;

public partial class RollCallSettingsDialog : Window
{
    private static readonly HashSet<string> SilentVoices = new(StringComparer.OrdinalIgnoreCase)
    {
        "Microsoft Zira Desktop",
        "Microsoft David Desktop"
    };
    private readonly string _initialVoiceId;
    private readonly string _initialOutputId;

    public bool RollCallShowId { get; private set; }
    public bool RollCallShowName { get; private set; }
    public bool RollCallRemoteEnabled { get; private set; }
    public string RemotePresenterKey { get; private set; } = "tab";
    public bool RollCallShowPhoto { get; private set; }
    public int RollCallPhotoDurationSeconds { get; private set; }
    public string RollCallPhotoSharedClass { get; private set; } = string.Empty;
    public bool RollCallTimerSoundEnabled { get; private set; }
    public bool RollCallTimerReminderEnabled { get; private set; }
    public int RollCallTimerReminderIntervalMinutes { get; private set; }
    public bool RollCallSpeechEnabled { get; private set; }
    public string RollCallTimerSoundVariant { get; private set; } = "gentle";
    public string RollCallTimerReminderSoundVariant { get; private set; } = "soft_beep";
    public string RollCallSpeechEngine { get; private set; } = "pyttsx3";
    public string RollCallSpeechVoiceId { get; private set; } = string.Empty;
    public string RollCallSpeechOutputId { get; private set; } = string.Empty;

    public RollCallSettingsDialog(AppSettings settings, IReadOnlyList<string> availableClasses)
    {
        InitializeComponent();
        _initialVoiceId = settings.RollCallSpeechVoiceId ?? string.Empty;
        _initialOutputId = settings.RollCallSpeechOutputId ?? string.Empty;
        ShowIdCheck.IsChecked = settings.RollCallShowId;
        ShowNameCheck.IsChecked = settings.RollCallShowName;
        ShowPhotoCheck.IsChecked = settings.RollCallShowPhoto;
        PhotoDurationSlider.Value = Math.Max(0, Math.Min(10, settings.RollCallPhotoDurationSeconds));
        BuildPhotoSharedCombo(availableClasses, settings.RollCallPhotoSharedClass);

        SpeechCheck.IsChecked = settings.RollCallSpeechEnabled;
        BuildSpeechEngineCombo(settings.RollCallSpeechEngine);
        BuildVoiceCombo(settings.RollCallSpeechVoiceId);
        BuildOutputCombo(settings.RollCallSpeechEngine, settings.RollCallSpeechOutputId);

        TimerSoundCheck.IsChecked = settings.RollCallTimerSoundEnabled;
        BuildTimerSoundCombo(settings.RollCallTimerSoundVariant);
        RollCallTimerSoundVariant = settings.RollCallTimerSoundVariant ?? "gentle";

        ReminderSoundCheck.IsChecked = settings.RollCallTimerReminderEnabled;
        BuildReminderSoundCombo(settings.RollCallTimerReminderSoundVariant);
        RollCallTimerReminderSoundVariant = settings.RollCallTimerReminderSoundVariant ?? "soft_beep";
        var interval = settings.RollCallTimerReminderIntervalMinutes;
        if (interval <= 0)
        {
            interval = 3;
        }
        ReminderIntervalSlider.Value = Math.Max(1, Math.Min(20, interval));
        RemoteEnabledCheck.IsChecked = settings.RollCallRemoteEnabled;
        BuildRemoteKeyCombo(settings.RemotePresenterKey);

        UpdatePhotoDurationLabel();
        UpdatePhotoControls();
        UpdateTimerControls();
        UpdateReminderIntervalLabel();
        UpdateSpeechControls();
        UpdateRemoteKeyEnabled();
        Loaded += (_, _) => WindowPlacementHelper.EnsureVisible(this);
    }

    private void OnRemoteEnabledChanged(object sender, RoutedEventArgs e)
    {
        UpdateRemoteKeyEnabled();
    }

    private void OnShowPhotoChanged(object sender, RoutedEventArgs e)
    {
        UpdatePhotoControls();
    }

    private void OnPhotoDurationChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdatePhotoDurationLabel();
    }

    private void OnTimerControlChanged(object sender, RoutedEventArgs e)
    {
        UpdateTimerControls();
    }

    private void OnReminderIntervalChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateReminderIntervalLabel();
    }

    private void OnSpeechEngineChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        UpdateSpeechControls();
    }

    private void UpdateRemoteKeyEnabled()
    {
        var enabled = RemoteEnabledCheck.IsChecked == true;
        RemoteKeyCombo.IsEnabled = enabled;
        if (RemoteKeyCustomButton != null)
        {
            RemoteKeyCustomButton.IsEnabled = enabled;
        }
    }

    private void UpdatePhotoControls()
    {
        var enabled = ShowPhotoCheck.IsChecked == true;
        PhotoDurationSlider.IsEnabled = enabled;
        PhotoSharedCombo.IsEnabled = enabled;
    }

    private void UpdatePhotoDurationLabel()
    {
        var seconds = (int)Math.Round(PhotoDurationSlider.Value);
        PhotoDurationLabel.Text = seconds <= 0 ? "不自动关闭" : $"{seconds} 秒";
    }

    private void UpdateTimerControls()
    {
        TimerSoundCombo.IsEnabled = TimerSoundCheck.IsChecked == true;
        var reminderEnabled = ReminderSoundCheck.IsChecked == true;
        ReminderSoundCombo.IsEnabled = reminderEnabled;
        ReminderIntervalSlider.IsEnabled = reminderEnabled;
    }

    private void UpdateSpeechControls()
    {
        var engine = GetSelectedValue(SpeechEngineCombo, "pyttsx3");
        if (engine == "pyttsx3")
        {
            SpeechOutputCombo.IsEnabled = false;
            SpeechOutputCombo.ToolTip = "pyttsx3 不支持切换输出设备。";
        }
        else if (SpeechOutputCombo.Items.Count > 0)
        {
            SpeechOutputCombo.IsEnabled = true;
            SpeechOutputCombo.ToolTip = string.Empty;
        }
        else
        {
            SpeechOutputCombo.IsEnabled = false;
            SpeechOutputCombo.ToolTip = "未检测到可用的输出设备。";
        }
    }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        var keyText = GetRemoteKey();
        if (string.IsNullOrWhiteSpace(keyText))
        {
            keyText = "tab";
        }
        if (RemoteEnabledCheck.IsChecked == true)
        {
            if (!KeyBindingParser.TryParse(keyText, out var binding) || binding == null)
            {
                System.Windows.MessageBox.Show("请输入有效的按键组合。", "提示", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }
            keyText = binding.ToString();
        }
        RollCallShowId = ShowIdCheck.IsChecked == true;
        RollCallShowName = ShowNameCheck.IsChecked == true;
        RollCallShowPhoto = ShowPhotoCheck.IsChecked == true;
        RollCallPhotoDurationSeconds = (int)Math.Round(PhotoDurationSlider.Value);
        RollCallPhotoSharedClass = GetSelectedValue(PhotoSharedCombo, string.Empty);
        RollCallTimerSoundEnabled = TimerSoundCheck.IsChecked == true;
        RollCallTimerSoundVariant = GetSelectedValue(TimerSoundCombo, "gentle");
        RollCallTimerReminderEnabled = ReminderSoundCheck.IsChecked == true;
        RollCallTimerReminderIntervalMinutes = (int)Math.Round(ReminderIntervalSlider.Value);
        RollCallTimerReminderSoundVariant = GetSelectedValue(ReminderSoundCombo, "soft_beep");
        RollCallSpeechEnabled = SpeechCheck.IsChecked == true;
        RollCallSpeechEngine = GetSelectedValue(SpeechEngineCombo, "pyttsx3");
        RollCallSpeechVoiceId = GetSelectedValue(SpeechVoiceCombo, _initialVoiceId);
        RollCallSpeechOutputId = GetSelectedValue(SpeechOutputCombo, _initialOutputId);
        RollCallRemoteEnabled = RemoteEnabledCheck.IsChecked == true;
        RemotePresenterKey = keyText;
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void BuildPhotoSharedCombo(IReadOnlyList<string> classes, string? current)
    {
        var items = new List<ComboOption>
        {
            new(string.Empty, "各班使用各自文件夹中的照片")
        };
        if (classes != null)
        {
            foreach (var name in classes)
            {
                if (string.IsNullOrWhiteSpace(name) || name == "全部")
                {
                    continue;
                }
                items.Add(new ComboOption(name, $"共用{name}照片文件夹"));
            }
        }
        PhotoSharedCombo.ItemsSource = items;
        PhotoSharedCombo.DisplayMemberPath = nameof(ComboOption.Label);
        PhotoSharedCombo.SelectedValuePath = nameof(ComboOption.Value);
        PhotoSharedCombo.SelectedValue = current ?? string.Empty;
    }

    private void BuildRemoteKeyCombo(string? current)
    {
        var items = new[]
        {
            new ComboOption("tab", "Tab键（切换超链接）"),
            new ComboOption("enter", "Enter键"),
            new ComboOption("space", "Space键"),
            new ComboOption("pageup", "PageUp键"),
            new ComboOption("pagedown", "PageDown键"),
            new ComboOption("left", "方向键←"),
            new ComboOption("right", "方向键→"),
            new ComboOption("up", "方向键↑"),
            new ComboOption("down", "方向键↓"),
            new ComboOption("f5", "F5键"),
            new ComboOption("shift+b", "Shift+B键（黑屏）")
        };
        RemoteKeyCombo.ItemsSource = items;
        RemoteKeyCombo.DisplayMemberPath = nameof(ComboOption.Label);
        RemoteKeyCombo.SelectedValuePath = nameof(ComboOption.Value);
        RemoteKeyCombo.SelectedValue = current ?? "tab";
        if (!string.IsNullOrWhiteSpace(current) && !items.Any(item => item.Value.Equals(current, StringComparison.OrdinalIgnoreCase)))
        {
            RemoteKeyCombo.Text = current;
        }
    }

    private void BuildSpeechEngineCombo(string? current)
    {
        var items = new[]
        {
            new ComboOption("pyttsx3", "pyttsx3（默认，跟随系统输出）"),
            new ComboOption("sapi", "SAPI（win32com，可选输出设备）")
        };
        SpeechEngineCombo.ItemsSource = items;
        SpeechEngineCombo.DisplayMemberPath = nameof(ComboOption.Label);
        SpeechEngineCombo.SelectedValuePath = nameof(ComboOption.Value);
        SpeechEngineCombo.SelectedValue = string.IsNullOrWhiteSpace(current) ? "pyttsx3" : current;
    }

    private void BuildVoiceCombo(string? current)
    {
        var voices = new List<ComboOption>();
        try
        {
            using var synth = new SpeechSynthesizer();
            foreach (var voice in synth.GetInstalledVoices())
            {
                if (!voice.Enabled)
                {
                    continue;
                }
                var name = voice.VoiceInfo.Name;
                if (SilentVoices.Contains(name))
                {
                    continue;
                }
                voices.Add(new ComboOption(name, name));
            }
        }
        catch
        {
            voices.Clear();
        }
        if (voices.Count == 0)
        {
            voices.Add(new ComboOption(string.Empty, "暂无可选发音人"));
            SpeechVoiceCombo.IsEnabled = false;
        }
        else
        {
            SpeechVoiceCombo.IsEnabled = true;
        }
        SpeechVoiceCombo.ItemsSource = voices;
        SpeechVoiceCombo.DisplayMemberPath = nameof(ComboOption.Label);
        SpeechVoiceCombo.SelectedValuePath = nameof(ComboOption.Value);
        if (voices.Count == 0)
        {
            SpeechVoiceCombo.SelectedValue = string.Empty;
        }
        else
        {
            var target = string.IsNullOrWhiteSpace(current) ? _initialVoiceId : current;
            if (!voices.Any(option => option.Value.Equals(target, StringComparison.OrdinalIgnoreCase)))
            {
                target = voices[0].Value;
            }
            SpeechVoiceCombo.SelectedValue = target;
        }
    }

    private void BuildOutputCombo(string? engine, string? current)
    {
        var items = new List<ComboOption>();
        items.Add(new ComboOption(string.Empty, "当前引擎不支持输出选择"));
        SpeechOutputCombo.ItemsSource = items;
        SpeechOutputCombo.DisplayMemberPath = nameof(ComboOption.Label);
        SpeechOutputCombo.SelectedValuePath = nameof(ComboOption.Value);
        SpeechOutputCombo.SelectedValue = current ?? _initialOutputId;
        UpdateSpeechControls();
    }

    private void BuildTimerSoundCombo(string? current)
    {
        var items = new[]
        {
            new ComboOption("gentle", "柔和铃声"),
            new ComboOption("bell", "上课铃"),
            new ComboOption("digital", "电子滴答"),
            new ComboOption("buzz", "蜂鸣器"),
            new ComboOption("urgent", "紧张倒计时")
        };
        TimerSoundCombo.ItemsSource = items;
        TimerSoundCombo.DisplayMemberPath = nameof(ComboOption.Label);
        TimerSoundCombo.SelectedValuePath = nameof(ComboOption.Value);
        TimerSoundCombo.SelectedValue = current ?? "gentle";
    }

    private void BuildReminderSoundCombo(string? current)
    {
        var items = new[]
        {
            new ComboOption("soft_beep", "轻柔提示"),
            new ComboOption("ping", "清脆提示"),
            new ComboOption("chime", "简洁钟声"),
            new ComboOption("pulse", "节奏哔哔"),
            new ComboOption("short_bell", "短铃提示")
        };
        ReminderSoundCombo.ItemsSource = items;
        ReminderSoundCombo.DisplayMemberPath = nameof(ComboOption.Label);
        ReminderSoundCombo.SelectedValuePath = nameof(ComboOption.Value);
        ReminderSoundCombo.SelectedValue = current ?? "soft_beep";
    }

    private void UpdateReminderIntervalLabel()
    {
        if (ReminderIntervalLabel == null || ReminderIntervalSlider == null)
        {
            return;
        }
        var minutes = (int)Math.Round(ReminderIntervalSlider.Value);
        ReminderIntervalLabel.Text = $"每 {minutes} 分钟";
    }

    private string GetRemoteKey()
    {
        var selected = GetSelectedValue(RemoteKeyCombo, string.Empty);
        if (!string.IsNullOrWhiteSpace(selected))
        {
            return selected;
        }
        return (RemoteKeyCombo.Text ?? string.Empty).Trim().ToLowerInvariant();
    }

    private void OnRemoteKeyCustomClick(object sender, RoutedEventArgs e)
    {
        var current = GetRemoteKey();
        var dialog = new RemoteKeyDialog(string.IsNullOrWhiteSpace(current) ? "tab" : current)
        {
            Owner = this
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }
        var keyText = dialog.SelectedKey;
        RemoteKeyCombo.SelectedValue = keyText;
        RemoteKeyCombo.Text = keyText;
    }

    private static string GetSelectedValue(System.Windows.Controls.ComboBox combo, string fallback)
    {
        if (combo.SelectedValue is string value && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }
        return fallback;
    }

    private sealed record ComboOption(string Value, string Label);

    private void OnTitleBarDrag(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
        {
            DragMove();
        }
    }
}
