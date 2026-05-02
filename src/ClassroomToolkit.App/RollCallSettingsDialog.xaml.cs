using System.Windows;
using ClassroomToolkit.App.Settings;
using System.Linq;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.Services.Input;

namespace ClassroomToolkit.App;

public partial class RollCallSettingsDialog : Window
{
    private readonly IReadOnlyList<string> _availableClasses;
    private readonly string _defaultRemotePresenterKey;
    private readonly string _defaultRemoteGroupSwitchKey;
    private readonly int _defaultReminderIntervalMinutes;
    private readonly record struct DisplayTabState(
        bool ShowId,
        bool ShowName,
        bool ShowPhoto,
        int PhotoDurationSeconds,
        string PhotoSharedClass);

    private readonly record struct SpeechTabState(
        bool SpeechEnabled,
        string SpeechEngine,
        string SpeechVoiceId,
        string SpeechOutputId);

    private readonly record struct RemoteTabState(
        bool RemoteEnabled,
        string RemotePresenterKey,
        bool RemoteGroupSwitchEnabled,
        string RemoteGroupSwitchKey);

    private readonly record struct TimerTabState(
        bool TimerSoundEnabled,
        string TimerSoundVariant,
        bool ReminderSoundEnabled,
        string ReminderSoundVariant,
        int ReminderIntervalMinutes);

    private readonly string _initialVoiceId;
    private readonly string _initialOutputId;
    private bool _suppressDirtyTracking = true;
    private DisplayTabState _initialDisplayTabState;
    private SpeechTabState _initialSpeechTabState;
    private RemoteTabState _initialRemoteTabState;
    private TimerTabState _initialTimerTabState;

    public bool RollCallShowId { get; private set; }
    public bool RollCallShowName { get; private set; }
    public bool RollCallRemoteEnabled { get; private set; }
    public bool RollCallRemoteGroupSwitchEnabled { get; private set; }
    public string RemotePresenterKey { get; private set; } = "tab";
    public string RemoteGroupSwitchKey { get; private set; } = "enter";
    public bool RollCallShowPhoto { get; private set; }
    public int RollCallPhotoDurationSeconds { get; private set; }
    public string RollCallPhotoSharedClass { get; private set; } = string.Empty;
    public bool RollCallTimerSoundEnabled { get; private set; }
    public bool RollCallTimerReminderEnabled { get; private set; }
    public int RollCallTimerReminderIntervalMinutes { get; private set; }
    public bool RollCallSpeechEnabled { get; private set; }
    public string RollCallTimerSoundVariant { get; private set; } = "gentle";
    public string RollCallTimerReminderSoundVariant { get; private set; } = "soft_beep";
    public string RollCallSpeechEngine { get; private set; } = "sapi";
    public string RollCallSpeechVoiceId { get; private set; } = string.Empty;
    public string RollCallSpeechOutputId { get; private set; } = string.Empty;

    public RollCallSettingsDialog(AppSettings settings, IReadOnlyList<string> availableClasses)
    {
        ArgumentNullException.ThrowIfNull(settings);

        InitializeComponent();
        var defaults = new AppSettings();
        _defaultRemotePresenterKey = string.IsNullOrWhiteSpace(defaults.RemotePresenterKey) ? "tab" : defaults.RemotePresenterKey;
        _defaultRemoteGroupSwitchKey = string.IsNullOrWhiteSpace(defaults.RemoteGroupSwitchKey) ? "enter" : defaults.RemoteGroupSwitchKey;
        _defaultReminderIntervalMinutes = defaults.RollCallTimerReminderIntervalMinutes <= 0 ? 5 : defaults.RollCallTimerReminderIntervalMinutes;
        _availableClasses = availableClasses ?? Array.Empty<string>();
        _initialVoiceId = settings.RollCallSpeechVoiceId ?? string.Empty;
        _initialOutputId = settings.RollCallSpeechOutputId ?? string.Empty;
        ShowIdCheck.IsChecked = settings.RollCallShowId;
        ShowNameCheck.IsChecked = settings.RollCallShowName;
        ShowPhotoCheck.IsChecked = settings.RollCallShowPhoto;
        PhotoDurationSlider.Value = Math.Max(0, Math.Min(10, settings.RollCallPhotoDurationSeconds));
        BuildPhotoSharedCombo(_availableClasses, settings.RollCallPhotoSharedClass);

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
            interval = _defaultReminderIntervalMinutes;
        }
        ReminderIntervalSlider.Value = Math.Max(1, Math.Min(20, interval));
        RemoteEnabledCheck.IsChecked = settings.RollCallRemoteEnabled;
        BuildRemoteKeyCombo(settings.RemotePresenterKey);

        RemoteGroupSwitchCheck.IsChecked = settings.RollCallRemoteGroupSwitchEnabled;
        BuildRemoteGroupSwitchKeyCombo(settings.RemoteGroupSwitchKey);

        UpdatePhotoDurationLabel();
        UpdatePhotoControls();
        UpdateTimerControls();
        UpdateReminderIntervalLabel();
        UpdateSpeechControls();
        UpdateRemoteKeyEnabled();
        UpdateRemoteGroupSwitchEnabled();
        AttachDirtyTrackingHandlers();
        _initialDisplayTabState = CaptureDisplayTabState();
        _initialSpeechTabState = CaptureSpeechTabState();
        _initialRemoteTabState = CaptureRemoteTabState();
        _initialTimerTabState = CaptureTimerTabState();
        _suppressDirtyTracking = false;
        UpdateTabDirtyStates();
        Loaded += OnDialogLoaded;
        Closed += OnDialogClosed;
    }

    private void OnDialogLoaded(object sender, RoutedEventArgs e)
    {
        WindowPlacementHelper.EnsureVisible(this);
    }

    private void OnDialogClosed(object? sender, EventArgs e)
    {
        DetachDirtyTrackingHandlers();
        Loaded -= OnDialogLoaded;
        Closed -= OnDialogClosed;
    }

    private void OnRemoteEnabledChanged(object sender, RoutedEventArgs e)
    {
        UpdateRemoteKeyEnabled();
        UpdateTabDirtyStates();
    }

    private void OnRemoteGroupSwitchChanged(object sender, RoutedEventArgs e)
    {
        UpdateRemoteGroupSwitchEnabled();
        UpdateTabDirtyStates();
    }

    private void OnSpeechToggleChanged(object sender, RoutedEventArgs e)
    {
        UpdateSpeechControls();
        UpdateTabDirtyStates();
    }

    private void OnShowPhotoChanged(object sender, RoutedEventArgs e)
    {
        UpdatePhotoControls();
        UpdateTabDirtyStates();
    }

    private void OnPhotoDurationChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdatePhotoDurationLabel();
        UpdateTabDirtyStates();
    }

    private void OnTimerControlChanged(object sender, RoutedEventArgs e)
    {
        UpdateTimerControls();
        UpdateTabDirtyStates();
    }

    private void OnReminderIntervalChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateReminderIntervalLabel();
        UpdateTabDirtyStates();
    }

    private void OnSpeechEngineChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        UpdateSpeechControls();
        // 重新构建语音列表，因为不同引擎可能有不同的语音
        BuildVoiceCombo(_initialVoiceId);
        UpdateTabDirtyStates();
    }

    private void AttachDirtyTrackingHandlers()
    {
        ShowIdCheck.Checked += OnDirtyTrackingRoutedChanged;
        ShowIdCheck.Unchecked += OnDirtyTrackingRoutedChanged;
        ShowNameCheck.Checked += OnDirtyTrackingRoutedChanged;
        ShowNameCheck.Unchecked += OnDirtyTrackingRoutedChanged;
        SpeechCheck.Checked += OnDirtyTrackingRoutedChanged;
        SpeechCheck.Unchecked += OnDirtyTrackingRoutedChanged;

        PhotoSharedCombo.SelectionChanged += OnDirtyTrackingSelectionChanged;
        SpeechVoiceCombo.SelectionChanged += OnDirtyTrackingSelectionChanged;
        SpeechOutputCombo.SelectionChanged += OnDirtyTrackingSelectionChanged;
        RemoteKeyCombo.SelectionChanged += OnDirtyTrackingSelectionChanged;
        RemoteGroupSwitchKeyCombo.SelectionChanged += OnDirtyTrackingSelectionChanged;
        TimerSoundCombo.SelectionChanged += OnDirtyTrackingSelectionChanged;
        ReminderSoundCombo.SelectionChanged += OnDirtyTrackingSelectionChanged;
    }

    private void DetachDirtyTrackingHandlers()
    {
        ShowIdCheck.Checked -= OnDirtyTrackingRoutedChanged;
        ShowIdCheck.Unchecked -= OnDirtyTrackingRoutedChanged;
        ShowNameCheck.Checked -= OnDirtyTrackingRoutedChanged;
        ShowNameCheck.Unchecked -= OnDirtyTrackingRoutedChanged;
        SpeechCheck.Checked -= OnDirtyTrackingRoutedChanged;
        SpeechCheck.Unchecked -= OnDirtyTrackingRoutedChanged;

        PhotoSharedCombo.SelectionChanged -= OnDirtyTrackingSelectionChanged;
        SpeechVoiceCombo.SelectionChanged -= OnDirtyTrackingSelectionChanged;
        SpeechOutputCombo.SelectionChanged -= OnDirtyTrackingSelectionChanged;
        RemoteKeyCombo.SelectionChanged -= OnDirtyTrackingSelectionChanged;
        RemoteGroupSwitchKeyCombo.SelectionChanged -= OnDirtyTrackingSelectionChanged;
        TimerSoundCombo.SelectionChanged -= OnDirtyTrackingSelectionChanged;
        ReminderSoundCombo.SelectionChanged -= OnDirtyTrackingSelectionChanged;
    }

    private void OnDirtyTrackingRoutedChanged(object? sender, RoutedEventArgs e)
    {
        UpdateTabDirtyStates();
    }

    private void OnDirtyTrackingSelectionChanged(object? sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        UpdateTabDirtyStates();
    }

    private DisplayTabState CaptureDisplayTabState()
    {
        return new DisplayTabState(
            ShowId: ShowIdCheck.IsChecked == true,
            ShowName: ShowNameCheck.IsChecked == true,
            ShowPhoto: ShowPhotoCheck.IsChecked == true,
            PhotoDurationSeconds: (int)Math.Round(PhotoDurationSlider.Value),
            PhotoSharedClass: GetSelectedValue(PhotoSharedCombo, string.Empty));
    }

    private RemoteTabState CaptureRemoteTabState()
    {
        return new RemoteTabState(
            RemoteEnabled: RemoteEnabledCheck.IsChecked == true,
            RemotePresenterKey: GetRemoteKey(),
            RemoteGroupSwitchEnabled: RemoteGroupSwitchCheck.IsChecked == true,
            RemoteGroupSwitchKey: GetRemoteGroupSwitchKey());
    }

    private TimerTabState CaptureTimerTabState()
    {
        return new TimerTabState(
            TimerSoundEnabled: TimerSoundCheck.IsChecked == true,
            TimerSoundVariant: GetSelectedValue(TimerSoundCombo, "gentle"),
            ReminderSoundEnabled: ReminderSoundCheck.IsChecked == true,
            ReminderSoundVariant: GetSelectedValue(ReminderSoundCombo, "soft_beep"),
            ReminderIntervalMinutes: (int)Math.Round(ReminderIntervalSlider.Value));
    }

    private void ApplyDisplayTabState(DisplayTabState state)
    {
        _suppressDirtyTracking = true;
        try
        {
            ShowIdCheck.IsChecked = state.ShowId;
            ShowNameCheck.IsChecked = state.ShowName;
            ShowPhotoCheck.IsChecked = state.ShowPhoto;
            PhotoDurationSlider.Value = Math.Clamp(state.PhotoDurationSeconds, 0, 10);
            SelectComboValue(PhotoSharedCombo, state.PhotoSharedClass, string.Empty);
        }
        finally
        {
            _suppressDirtyTracking = false;
        }

        UpdatePhotoDurationLabel();
        UpdatePhotoControls();
    }

    private void ApplyRemoteTabState(RemoteTabState state)
    {
        _suppressDirtyTracking = true;
        try
        {
            RemoteEnabledCheck.IsChecked = state.RemoteEnabled;
            SelectComboValue(RemoteKeyCombo, state.RemotePresenterKey, _defaultRemotePresenterKey);
            RemoteGroupSwitchCheck.IsChecked = state.RemoteGroupSwitchEnabled;
            SelectComboValue(RemoteGroupSwitchKeyCombo, state.RemoteGroupSwitchKey, _defaultRemoteGroupSwitchKey);
        }
        finally
        {
            _suppressDirtyTracking = false;
        }

        UpdateRemoteKeyEnabled();
        UpdateRemoteGroupSwitchEnabled();
    }

    private void ApplyTimerTabState(TimerTabState state)
    {
        _suppressDirtyTracking = true;
        try
        {
            TimerSoundCheck.IsChecked = state.TimerSoundEnabled;
            SelectComboValue(TimerSoundCombo, state.TimerSoundVariant, "gentle");
            ReminderSoundCheck.IsChecked = state.ReminderSoundEnabled;
            SelectComboValue(ReminderSoundCombo, state.ReminderSoundVariant, "soft_beep");
            ReminderIntervalSlider.Value = Math.Clamp(state.ReminderIntervalMinutes, 1, 20);
        }
        finally
        {
            _suppressDirtyTracking = false;
        }

        UpdateTimerControls();
        UpdateReminderIntervalLabel();
    }

    private void UpdateTabDirtyStates()
    {
        if (_suppressDirtyTracking)
        {
            return;
        }

        SetTabHeader(SettingsTabs, 0, "显示", IsDisplayTabDirty());
        SetTabHeader(SettingsTabs, 1, "语音", IsSpeechTabDirty());
        SetTabHeader(SettingsTabs, 2, "遥控", IsRemoteTabDirty());
        SetTabHeader(SettingsTabs, 3, "提醒", IsTimerTabDirty());
        UpdateChangeSummaryText();
    }

    private bool IsDisplayTabDirty()
    {
        var current = CaptureDisplayTabState();
        var initial = _initialDisplayTabState;
        return current.ShowId != initial.ShowId
            || current.ShowName != initial.ShowName
            || current.ShowPhoto != initial.ShowPhoto
            || current.PhotoDurationSeconds != initial.PhotoDurationSeconds
            || !string.Equals(current.PhotoSharedClass, initial.PhotoSharedClass, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsRemoteTabDirty()
    {
        var current = CaptureRemoteTabState();
        var initial = _initialRemoteTabState;
        return current.RemoteEnabled != initial.RemoteEnabled
            || !string.Equals(current.RemotePresenterKey, initial.RemotePresenterKey, StringComparison.OrdinalIgnoreCase)
            || current.RemoteGroupSwitchEnabled != initial.RemoteGroupSwitchEnabled
            || !string.Equals(current.RemoteGroupSwitchKey, initial.RemoteGroupSwitchKey, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsTimerTabDirty()
    {
        var current = CaptureTimerTabState();
        var initial = _initialTimerTabState;
        return current.TimerSoundEnabled != initial.TimerSoundEnabled
            || !string.Equals(current.TimerSoundVariant, initial.TimerSoundVariant, StringComparison.OrdinalIgnoreCase)
            || current.ReminderSoundEnabled != initial.ReminderSoundEnabled
            || !string.Equals(current.ReminderSoundVariant, initial.ReminderSoundVariant, StringComparison.OrdinalIgnoreCase)
            || current.ReminderIntervalMinutes != initial.ReminderIntervalMinutes;
    }

    private void UpdateRemoteKeyEnabled()
    {
        var enabled = RemoteEnabledCheck.IsChecked == true;
        RemoteKeyCombo.IsEnabled = enabled;
        RemoteKeyCombo.ToolTip = enabled ? null : "开启后可设置点名按键。";
    }

    private void UpdateRemoteGroupSwitchEnabled()
    {
        var enabled = RemoteGroupSwitchCheck.IsChecked == true;
        RemoteGroupSwitchKeyCombo.IsEnabled = enabled;
        RemoteGroupSwitchKeyCombo.ToolTip = enabled ? null : "开启后可设置分组按键。";
    }

    private void UpdatePhotoControls()
    {
        var enabled = ShowPhotoCheck.IsChecked == true;
        PhotoDurationSlider.IsEnabled = enabled;
        PhotoSharedCombo.IsEnabled = enabled;
        var disabledTip = "开启后可设置照片时长和来源。";
        PhotoDurationSlider.ToolTip = enabled ? null : disabledTip;
        PhotoSharedCombo.ToolTip = enabled ? null : disabledTip;
    }

    private void UpdatePhotoDurationLabel()
    {
        var seconds = (int)Math.Round(PhotoDurationSlider.Value);
        PhotoDurationLabel.Text = seconds <= 0 ? "不自动关闭" : $"{seconds} 秒";
    }

    private void UpdateTimerControls()
    {
        TimerSoundCombo.IsEnabled = TimerSoundCheck.IsChecked == true;
        TimerSoundCombo.ToolTip = TimerSoundCheck.IsChecked == true ? null : "开启后可选择结束音效。";
        var reminderEnabled = ReminderSoundCheck.IsChecked == true;
        ReminderSoundCombo.IsEnabled = reminderEnabled;
        ReminderIntervalSlider.IsEnabled = reminderEnabled;
        var reminderTip = "开启后可设置提醒音效和间隔。";
        ReminderSoundCombo.ToolTip = reminderEnabled ? null : reminderTip;
        ReminderIntervalSlider.ToolTip = reminderEnabled ? null : reminderTip;
    }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        var keyText = GetRemoteKey();
        var groupKeyText = GetRemoteGroupSwitchKey();

        if (string.IsNullOrWhiteSpace(keyText)) keyText = _defaultRemotePresenterKey;
        if (string.IsNullOrWhiteSpace(groupKeyText)) groupKeyText = _defaultRemoteGroupSwitchKey;

        if (RemoteEnabledCheck.IsChecked == true)
        {
            if (!KeyBindingTokenParser.TryNormalize(keyText, out var normalizedKey))
            {
                System.Windows.MessageBox.Show("请输入有效的点名按键组合。", "提示", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }
            keyText = normalizedKey;
        }

        if (RemoteGroupSwitchCheck.IsChecked == true)
        {
            if (!KeyBindingTokenParser.TryNormalize(groupKeyText, out var normalizedGroupKey))
            {
                System.Windows.MessageBox.Show("请输入有效的分组切换按键组合。", "提示", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }
            groupKeyText = normalizedGroupKey;
        }

        if (RemoteEnabledCheck.IsChecked == true && RemoteGroupSwitchCheck.IsChecked == true &&
            string.Equals(keyText, groupKeyText, StringComparison.OrdinalIgnoreCase))
        {
            System.Windows.MessageBox.Show("点名按键和分组切换按键不能相同，请重新选择。", "冲突", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
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
        RollCallSpeechEngine = GetSelectedValue(SpeechEngineCombo, "sapi");
        RollCallSpeechVoiceId = GetSelectedValue(SpeechVoiceCombo, _initialVoiceId);
        RollCallSpeechOutputId = string.Empty;
        RollCallRemoteEnabled = RemoteEnabledCheck.IsChecked == true;
        RollCallRemoteGroupSwitchEnabled = RemoteGroupSwitchCheck.IsChecked == true;
        RemotePresenterKey = keyText;
        RemoteGroupSwitchKey = groupKeyText;
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void OnRestoreDefaultsClick(object sender, RoutedEventArgs e)
    {
        ApplyDefaultSettingsForCurrentTab();
    }

    private void OnRestoreAllDefaultsClick(object sender, RoutedEventArgs e)
    {
        var result = System.Windows.MessageBox.Show(
            "恢复点名设置为默认值，是否继续？",
            "重置全部设置",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);
        if (result != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        ApplyDefaultSettings();
    }

    private void ApplyDefaultSettingsForCurrentTab()
    {
        var defaults = new AppSettings();
        var tabIndex = SettingsTabs?.SelectedIndex ?? 0;
        _suppressDirtyTracking = true;
        try
        {
            switch (tabIndex)
            {
                case 0:
                    ShowIdCheck.IsChecked = defaults.RollCallShowId;
                    ShowNameCheck.IsChecked = defaults.RollCallShowName;
                    ShowPhotoCheck.IsChecked = defaults.RollCallShowPhoto;
                    PhotoDurationSlider.Value = Math.Clamp(defaults.RollCallPhotoDurationSeconds, 0, 10);
                    BuildPhotoSharedCombo(_availableClasses, defaults.RollCallPhotoSharedClass);
                    break;
                case 1:
                    SpeechCheck.IsChecked = defaults.RollCallSpeechEnabled;
                    BuildSpeechEngineCombo(defaults.RollCallSpeechEngine);
                    BuildVoiceCombo(defaults.RollCallSpeechVoiceId);
                    SelectComboValue(SpeechVoiceCombo, defaults.RollCallSpeechVoiceId, string.Empty);
                    BuildOutputCombo(defaults.RollCallSpeechEngine, defaults.RollCallSpeechOutputId);
                    SelectComboValue(SpeechOutputCombo, defaults.RollCallSpeechOutputId, string.Empty);
                    break;
                case 2:
                    RemoteEnabledCheck.IsChecked = defaults.RollCallRemoteEnabled;
                    BuildRemoteKeyCombo(defaults.RemotePresenterKey);
                    RemoteGroupSwitchCheck.IsChecked = defaults.RollCallRemoteGroupSwitchEnabled;
                    BuildRemoteGroupSwitchKeyCombo(defaults.RemoteGroupSwitchKey);
                    break;
                case 3:
                    TimerSoundCheck.IsChecked = defaults.RollCallTimerSoundEnabled;
                    BuildTimerSoundCombo(defaults.RollCallTimerSoundVariant);
                    ReminderSoundCheck.IsChecked = defaults.RollCallTimerReminderEnabled;
                    BuildReminderSoundCombo(defaults.RollCallTimerReminderSoundVariant);
                    var reminderInterval = defaults.RollCallTimerReminderIntervalMinutes <= 0
                        ? _defaultReminderIntervalMinutes
                        : defaults.RollCallTimerReminderIntervalMinutes;
                    ReminderIntervalSlider.Value = Math.Clamp(reminderInterval, 1, 20);
                    break;
                default:
                    ApplyDefaultSettings();
                    return;
            }
        }
        finally
        {
            _suppressDirtyTracking = false;
        }

        UpdatePhotoDurationLabel();
        UpdatePhotoControls();
        UpdateTimerControls();
        UpdateReminderIntervalLabel();
        UpdateSpeechControls();
        UpdateRemoteKeyEnabled();
        UpdateRemoteGroupSwitchEnabled();
        UpdateTabDirtyStates();
    }

    private void ApplyDefaultSettings()
    {
        var defaults = new AppSettings();
        _suppressDirtyTracking = true;
        try
        {
            ShowIdCheck.IsChecked = defaults.RollCallShowId;
            ShowNameCheck.IsChecked = defaults.RollCallShowName;
            ShowPhotoCheck.IsChecked = defaults.RollCallShowPhoto;
            PhotoDurationSlider.Value = Math.Clamp(defaults.RollCallPhotoDurationSeconds, 0, 10);
            BuildPhotoSharedCombo(_availableClasses, defaults.RollCallPhotoSharedClass);

            SpeechCheck.IsChecked = defaults.RollCallSpeechEnabled;
            BuildSpeechEngineCombo(defaults.RollCallSpeechEngine);
            BuildVoiceCombo(defaults.RollCallSpeechVoiceId);
            SelectComboValue(SpeechVoiceCombo, defaults.RollCallSpeechVoiceId, string.Empty);
            BuildOutputCombo(defaults.RollCallSpeechEngine, defaults.RollCallSpeechOutputId);
            SelectComboValue(SpeechOutputCombo, defaults.RollCallSpeechOutputId, string.Empty);

            TimerSoundCheck.IsChecked = defaults.RollCallTimerSoundEnabled;
            BuildTimerSoundCombo(defaults.RollCallTimerSoundVariant);
            ReminderSoundCheck.IsChecked = defaults.RollCallTimerReminderEnabled;
            BuildReminderSoundCombo(defaults.RollCallTimerReminderSoundVariant);
            var reminderInterval = defaults.RollCallTimerReminderIntervalMinutes <= 0
                ? _defaultReminderIntervalMinutes
                : defaults.RollCallTimerReminderIntervalMinutes;
            ReminderIntervalSlider.Value = Math.Clamp(reminderInterval, 1, 20);

            RemoteEnabledCheck.IsChecked = defaults.RollCallRemoteEnabled;
            BuildRemoteKeyCombo(defaults.RemotePresenterKey);
            RemoteGroupSwitchCheck.IsChecked = defaults.RollCallRemoteGroupSwitchEnabled;
            BuildRemoteGroupSwitchKeyCombo(defaults.RemoteGroupSwitchKey);
        }
        finally
        {
            _suppressDirtyTracking = false;
        }

        UpdatePhotoDurationLabel();
        UpdatePhotoControls();
        UpdateTimerControls();
        UpdateReminderIntervalLabel();
        UpdateSpeechControls();
        UpdateRemoteKeyEnabled();
        UpdateRemoteGroupSwitchEnabled();
        UpdateTabDirtyStates();
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
        var items = GetRemoteKeyOptions();
        RemoteKeyCombo.ItemsSource = items;
        RemoteKeyCombo.DisplayMemberPath = nameof(ComboOption.Label);
        RemoteKeyCombo.SelectedValuePath = nameof(ComboOption.Value);
        var selected = string.IsNullOrWhiteSpace(current) ? _defaultRemotePresenterKey : current;
        if (!items.Any(item => item.Value.Equals(selected, StringComparison.OrdinalIgnoreCase)))
        {
            selected = _defaultRemotePresenterKey;
        }
        RemoteKeyCombo.SelectedValue = selected;
    }

    private void BuildRemoteGroupSwitchKeyCombo(string? current)
    {
        var items = GetRemoteKeyOptions();
        RemoteGroupSwitchKeyCombo.ItemsSource = items;
        RemoteGroupSwitchKeyCombo.DisplayMemberPath = nameof(ComboOption.Label);
        RemoteGroupSwitchKeyCombo.SelectedValuePath = nameof(ComboOption.Value);
        var selected = string.IsNullOrWhiteSpace(current) ? _defaultRemoteGroupSwitchKey : current;
        if (!items.Any(item => item.Value.Equals(selected, StringComparison.OrdinalIgnoreCase)))
        {
            selected = _defaultRemoteGroupSwitchKey;
        }
        RemoteGroupSwitchKeyCombo.SelectedValue = selected;
    }

    private static ComboOption[] GetRemoteKeyOptions()
    {
        return new[]
        {
            new ComboOption("tab", "Tab键（推荐）"),
            new ComboOption("enter", "Enter键（推荐切组）"),
            new ComboOption("f5", "F5/Shift+F5/Esc键（全屏/退出全屏）"),
            new ComboOption("b", "B/b键（黑屏）")
        };
    }

    private void BuildTimerSoundCombo(string? current)
    {
        var items = new[]
        {
            new ComboOption("bell", "上课铃"),
            new ComboOption("gentle", "下课铃（推荐）"),
            new ComboOption("digital", "闹钟"),
            new ComboOption("buzz", "门铃")
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
            new ComboOption("short_bell", "轻柔铃声"),
            new ComboOption("chime", "提醒钟"),
            new ComboOption("soft_beep", "短提示音（推荐）")
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
        return (RemoteKeyCombo.Text ?? string.Empty).Trim();
    }

    private string GetRemoteGroupSwitchKey()
    {
        var selected = GetSelectedValue(RemoteGroupSwitchKeyCombo, string.Empty);
        if (!string.IsNullOrWhiteSpace(selected))
        {
            return selected;
        }
        return (RemoteGroupSwitchKeyCombo.Text ?? string.Empty).Trim();
    }

    private static string GetSelectedValue(System.Windows.Controls.ComboBox combo, string fallback)
    {
        if (combo.SelectedValue is string value && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }
        return fallback;
    }

    private static void SelectComboValue(System.Windows.Controls.ComboBox combo, string value, string fallback)
    {
        if (combo.ItemsSource == null)
        {
            combo.SelectedValue = string.IsNullOrWhiteSpace(value) ? fallback : value;
            return;
        }

        combo.SelectedValue = string.IsNullOrWhiteSpace(value) ? fallback : value;
        var selected = GetSelectedValue(combo, string.Empty);
        if (!string.IsNullOrWhiteSpace(selected))
        {
            return;
        }

        combo.SelectedValue = fallback;
    }

    private void UpdateChangeSummaryText()
    {
        if (ChangeSummaryText == null)
        {
            return;
        }

        var dirtyTabs = new List<string>(4);
        if (IsDisplayTabDirty())
        {
            dirtyTabs.Add("显示");
        }
        if (IsSpeechTabDirty())
        {
            dirtyTabs.Add("语音");
        }
        if (IsRemoteTabDirty())
        {
            dirtyTabs.Add("遥控");
        }
        if (IsTimerTabDirty())
        {
            dirtyTabs.Add("提醒");
        }

        ChangeSummaryText.Text = dirtyTabs.Count == 0
            ? "本次未修改设置。"
            : $"本次已修改：{string.Join("、", dirtyTabs)}。";
    }

    private static void SetTabHeader(System.Windows.Controls.TabControl? tabs, int index, string baseHeader, bool isDirty)
    {
        if (tabs == null || index < 0 || index >= tabs.Items.Count)
        {
            return;
        }

        if (tabs.Items[index] is not System.Windows.Controls.TabItem tabItem)
        {
            return;
        }

        tabItem.Header = isDirty ? $"{baseHeader} *" : baseHeader;
    }

    private sealed record ComboOption(string Value, string Label);

    private void OnTitleBarDrag(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
        {
            _ = this.SafeDragMove();
        }
    }
}
