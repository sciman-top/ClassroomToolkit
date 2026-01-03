using System.Globalization;
using System.Speech.Synthesis;
using System.Windows;
using ClassroomToolkit.App.Settings;
using ClassroomToolkit.Interop.Presentation;
using System.Linq;
using ClassroomToolkit.App.Helpers;
using Microsoft.Win32;

namespace ClassroomToolkit.App;

public partial class RollCallSettingsDialog : Window
{
    private static readonly HashSet<string> SilentVoices = new(StringComparer.OrdinalIgnoreCase)
    {
        // 完全清空过滤列表，确保显示所有语音
        // 如果需要过滤，请基于实际的语音名称进行过滤
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
        // 重新构建语音列表，因为不同引擎可能有不同的语音
        BuildVoiceCombo(_initialVoiceId);
    }

    private void UpdateRemoteKeyEnabled()
    {
        var enabled = RemoteEnabledCheck.IsChecked == true;
        RemoteKeyCombo.IsEnabled = enabled;
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
            new ComboOption("shift+f5", "Shift+F5键（从当前页放映）"),
            new ComboOption("esc", "Esc键（退出放映）"),
            new ComboOption("b", "B键（黑屏）"),
            new ComboOption("w", "W键（白屏）"),
            new ComboOption("shift+b", "Shift+B键（黑屏）")
        };
        RemoteKeyCombo.ItemsSource = items;
        RemoteKeyCombo.DisplayMemberPath = nameof(ComboOption.Label);
        RemoteKeyCombo.SelectedValuePath = nameof(ComboOption.Value);
        var selected = string.IsNullOrWhiteSpace(current) ? "tab" : current;
        if (!items.Any(item => item.Value.Equals(selected, StringComparison.OrdinalIgnoreCase)))
        {
            selected = "tab";
        }
        RemoteKeyCombo.SelectedValue = selected;
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

    private void BuildSapiVoices(List<ComboOption> voices)
    {
        using var synth = new SpeechSynthesizer();
        var allVoices = synth.GetInstalledVoices().ToList();
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var voice in allVoices)
        {
            var info = voice.VoiceInfo;
            if (!voice.Enabled)
            {
                continue;
            }
            existing.Add(info.Name);
            var label = $"{info.Name} ({info.Culture.Name}, {info.Gender})";
            voices.Add(new ComboOption(info.Name, label));
        }

        if (voices.Count == 0)
        {
            voices.Add(new ComboOption(string.Empty, "暂无可用发音人"));
        }
    }

    private void BuildVoiceCombo(string? current)
    {
        var voices = new List<ComboOption>();
        var engine = GetSelectedValue(SpeechEngineCombo, "pyttsx3");
        
        try
        {
            if (engine == "pyttsx3")
            {
                // 对于 pyttsx3，我们需要通过 Python 获取语音列表
                // 这里先使用 SAPI 作为后备，显示所有可用的语音
                BuildSapiVoices(voices);
            }
            else
            {
                // 对于 SAPI，直接使用 SpeechSynthesizer
                BuildSapiVoices(voices);
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

    /// <summary>
    /// 格式化发音人标签，显示名称、语言和性别信息
    /// </summary>
    private static string FormatVoiceLabel(System.Speech.Synthesis.VoiceInfo info, bool isChinese, bool isEnabled)
    {
        var languageName = GetLanguageDisplayName(info.Culture.Name);
        var gender = GetGenderDisplayName(info);

        // 中文发音人优先，在标签前加"【推荐】"
        var prefix = isChinese ? "【推荐】" : "";
        var suffix = isEnabled ? string.Empty : "（未启用）";

        // 格式：名称（语言，性别）
        return $"{prefix}{info.Name}（{languageName}，{gender}）{suffix}";
    }

    /// <summary>
    /// 获取语言的显示名称
    /// </summary>
    private static string GetLanguageDisplayName(string cultureName)
    {
        try
        {
            var culture = new System.Globalization.CultureInfo(cultureName);
            var nativeName = culture.NativeName; // 本地语言名称
            var englishName = culture.EnglishName; // 英文名称

            // 如果本地名称和英文名称不同，显示两个
            if (nativeName != englishName && !string.IsNullOrWhiteSpace(nativeName))
            {
                // 提取本地名称的第一部分（避免显示过于冗长）
                var nativeShort = nativeName.Split('(')[0].Trim();
                return $"{englishName}·{nativeShort}";
            }

            return englishName;
        }
        catch
        {
            return cultureName.ToUpperInvariant();
        }
    }

    /// <summary>
    /// 获取性别的显示名称
    /// </summary>
    private static string GetGenderDisplayName(System.Speech.Synthesis.VoiceInfo info)
    {
        return info.Gender switch
        {
            System.Speech.Synthesis.VoiceGender.Male => "男",
            System.Speech.Synthesis.VoiceGender.Female => "女",
            System.Speech.Synthesis.VoiceGender.Neutral => "中性",
            _ => "未知"
        };
    }

    private sealed record RegistryVoice(string Name, string CultureName, string Gender, bool Enabled);

    private static IEnumerable<RegistryVoice> ReadRegistryVoices()
    {
        var results = new List<RegistryVoice>();
        ReadRegistryVoices(results, RegistryHive.LocalMachine, RegistryView.Registry64);
        ReadRegistryVoices(results, RegistryHive.LocalMachine, RegistryView.Registry32);
        ReadRegistryVoices(results, RegistryHive.CurrentUser, RegistryView.Registry64);
        ReadRegistryVoices(results, RegistryHive.CurrentUser, RegistryView.Registry32);
        return results;
    }

    private static void ReadRegistryVoices(List<RegistryVoice> results, RegistryHive hive, RegistryView view)
    {
        ReadRegistryVoicePath(results, hive, view, @"SOFTWARE\Microsoft\Speech\Voices\Tokens");
        ReadRegistryVoicePath(results, hive, view, @"SOFTWARE\Microsoft\Speech_OneCore\Voices\Tokens");
    }

    private static void ReadRegistryVoicePath(List<RegistryVoice> results, RegistryHive hive, RegistryView view, string path)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var root = baseKey.OpenSubKey(path);
            if (root == null)
            {
                return;
            }
            foreach (var tokenName in root.GetSubKeyNames())
            {
                using var tokenKey = root.OpenSubKey(tokenName);
                if (tokenKey == null)
                {
                    continue;
                }
                var name = ReadRegistryValue(tokenKey, "Name");
                using var attributesKey = tokenKey.OpenSubKey("Attributes");
                if (string.IsNullOrWhiteSpace(name))
                {
                    name = ReadRegistryValue(attributesKey, "Name");
                }
                if (string.IsNullOrWhiteSpace(name))
                {
                    name = tokenName;
                }
                var cultureName = ReadRegistryValue(tokenKey, "Language");
                if (string.IsNullOrWhiteSpace(cultureName))
                {
                    cultureName = ReadRegistryValue(attributesKey, "Language");
                }
                cultureName = NormalizeCultureName(cultureName);
                var gender = ReadRegistryValue(tokenKey, "Gender");
                if (string.IsNullOrWhiteSpace(gender))
                {
                    gender = ReadRegistryValue(attributesKey, "Gender");
                }
                var enabled = true;
                var enabledValue = ReadRegistryValue(tokenKey, "Enabled");
                if (!string.IsNullOrWhiteSpace(enabledValue) && int.TryParse(enabledValue, out var enabledInt))
                {
                    enabled = enabledInt != 0;
                }
                results.Add(new RegistryVoice(name, cultureName, gender, enabled));
            }
        }
        catch
        {
            // Ignore registry access errors to avoid breaking the settings dialog.
        }
    }

    private static string ReadRegistryValue(RegistryKey? key, string name)
    {
        if (key == null)
        {
            return string.Empty;
        }
        var value = key.GetValue(name);
        return value?.ToString() ?? string.Empty;
    }

    private static string NormalizeCultureName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }
        var trimmed = value.Trim();
        if (trimmed.Contains('-'))
        {
            return trimmed;
        }
        if (int.TryParse(trimmed, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var lcid) ||
            int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out lcid))
        {
            try
            {
                return new CultureInfo(lcid).Name;
            }
            catch
            {
                return string.Empty;
            }
        }
        return string.Empty;
    }

    private static string FormatRegistryVoiceLabel(RegistryVoice voice)
    {
        var languageName = string.IsNullOrWhiteSpace(voice.CultureName)
            ? "未知语言"
            : GetLanguageDisplayName(voice.CultureName);
        var gender = string.IsNullOrWhiteSpace(voice.Gender) ? "未知" : voice.Gender;
        var suffix = voice.Enabled ? string.Empty : "（未启用）";
        return $"{voice.Name}（{languageName}，{gender}）{suffix}";
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
