using System.Globalization;
using System.Speech.Synthesis;
using System.Windows;
using ClassroomToolkit.App.Settings;
using System.Linq;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.Services.Input;
using Microsoft.Win32;

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

    private SpeechTabState CaptureSpeechTabState()
    {
        return new SpeechTabState(
            SpeechEnabled: SpeechCheck.IsChecked == true,
            SpeechEngine: GetSelectedValue(SpeechEngineCombo, "sapi"),
            SpeechVoiceId: GetSelectedValue(SpeechVoiceCombo, string.Empty),
            SpeechOutputId: GetSelectedValue(SpeechOutputCombo, string.Empty));
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

    private void ApplySpeechTabState(SpeechTabState state)
    {
        _suppressDirtyTracking = true;
        try
        {
            SpeechCheck.IsChecked = state.SpeechEnabled;
            BuildSpeechEngineCombo(state.SpeechEngine);
            BuildVoiceCombo(state.SpeechVoiceId);
            BuildOutputCombo(state.SpeechEngine, state.SpeechOutputId);
            SelectComboValue(SpeechVoiceCombo, state.SpeechVoiceId, _initialVoiceId);
            SelectComboValue(SpeechOutputCombo, state.SpeechOutputId, _initialOutputId);
        }
        finally
        {
            _suppressDirtyTracking = false;
        }

        UpdateSpeechControls();
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

    private bool IsSpeechTabDirty()
    {
        var current = CaptureSpeechTabState();
        var initial = _initialSpeechTabState;
        return current.SpeechEnabled != initial.SpeechEnabled
            || !string.Equals(current.SpeechEngine, initial.SpeechEngine, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(current.SpeechVoiceId, initial.SpeechVoiceId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(current.SpeechOutputId, initial.SpeechOutputId, StringComparison.OrdinalIgnoreCase);
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

    private void UpdateSpeechControls()
    {
        var speechEnabled = SpeechCheck.IsChecked == true;
        SpeechEngineCombo.IsEnabled = speechEnabled;
        SpeechVoiceCombo.IsEnabled = speechEnabled;
        if (!speechEnabled)
        {
            SpeechOutputCombo.IsEnabled = false;
            SpeechOutputCombo.ToolTip = "已关闭语音播报。";
            return;
        }

        SpeechOutputCombo.IsEnabled = false;
        SpeechOutputCombo.ToolTip = "当前版本暂不支持播报设备选择。";

        if (SpeechVoiceCombo.Items.Count == 0)
        {
            SpeechVoiceCombo.IsEnabled = false;
        }
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

    private static IReadOnlyList<ComboOption> GetRemoteKeyOptions()
    {
        return new[]
        {
            new ComboOption("tab", "Tab键（推荐）"),
            new ComboOption("enter", "Enter键（推荐切组）"),
            new ComboOption("f5", "F5/Shift+F5/Esc键（全屏/退出全屏）"),
            new ComboOption("b", "B/b键（黑屏）")
        };
    }

    private void BuildSpeechEngineCombo(string? current)
    {
        var items = new[]
        {
            new ComboOption("sapi", "系统语音（SAPI）")
        };
        SpeechEngineCombo.ItemsSource = items;
        SpeechEngineCombo.DisplayMemberPath = nameof(ComboOption.Label);
        SpeechEngineCombo.SelectedValuePath = nameof(ComboOption.Value);
        SpeechEngineCombo.SelectedValue = "sapi";
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
        try
        {
            BuildSapiVoices(voices);
        }
        catch (Exception caughtEx) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(caughtEx))
        {
            voices.Clear();
        }

        if (voices.Count == 0)
        {
            voices.Add(new ComboOption(string.Empty, "暂无可选发音人"));
        }

        SpeechVoiceCombo.ItemsSource = voices;
        SpeechVoiceCombo.DisplayMemberPath = nameof(ComboOption.Label);
        SpeechVoiceCombo.SelectedValuePath = nameof(ComboOption.Value);

        var decision = RollCallVoiceSelectionPolicy.Resolve(
            voices.Select(option => option.Value).ToList(),
            preferredVoiceId: current,
            fallbackVoiceId: _initialVoiceId);

        SpeechVoiceCombo.IsEnabled = decision.IsVoiceSelectionEnabled;
        SpeechVoiceCombo.SelectedValue = decision.SelectedVoiceId;
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
        catch (Exception caughtEx) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(caughtEx))
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
        catch (Exception caughtEx) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(caughtEx))
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
            catch (Exception caughtEx) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(caughtEx))
            {
                return string.Empty;
            }
        }
        return string.Empty;
    }

    private void BuildOutputCombo(string? engine, string? current)
    {
        var items = new List<ComboOption>();
        items.Add(new ComboOption(string.Empty, "当前版本暂不支持输出设备选择"));
        SpeechOutputCombo.ItemsSource = items;
        SpeechOutputCombo.DisplayMemberPath = nameof(ComboOption.Label);
        SpeechOutputCombo.SelectedValuePath = nameof(ComboOption.Value);
        SpeechOutputCombo.SelectedValue = string.Empty;
        UpdateSpeechControls();
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
