using System.Windows;
using ClassroomToolkit.App.Settings;
using ClassroomToolkit.Interop.Presentation;

namespace ClassroomToolkit.App;

public partial class RollCallSettingsDialog : Window
{
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

    public RollCallSettingsDialog(AppSettings settings)
    {
        InitializeComponent();
        ShowIdCheck.IsChecked = settings.RollCallShowId;
        ShowNameCheck.IsChecked = settings.RollCallShowName;
        ShowPhotoCheck.IsChecked = settings.RollCallShowPhoto;
        PhotoDurationSlider.Value = Math.Max(0, Math.Min(10, settings.RollCallPhotoDurationSeconds));
        PhotoSharedBox.Text = settings.RollCallPhotoSharedClass ?? string.Empty;
        TimerSoundCheck.IsChecked = settings.RollCallTimerSoundEnabled;
        TimerReminderCheck.IsChecked = settings.RollCallTimerReminderEnabled;
        TimerReminderCombo.ItemsSource = new[] { "1", "3", "5", "10" };
        TimerReminderCombo.Text = settings.RollCallTimerReminderIntervalMinutes.ToString();
        RemoteEnabledCheck.IsChecked = settings.RollCallRemoteEnabled;
        RemoteKeyCombo.ItemsSource = new[] { "tab", "shift+b" };
        RemoteKeyCombo.Text = settings.RemotePresenterKey;
        UpdatePhotoDurationLabel();
        UpdatePhotoControls();
        UpdateTimerReminderControls();
        UpdateRemoteKeyEnabled();
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

    private void OnTimerReminderChanged(object sender, RoutedEventArgs e)
    {
        UpdateTimerReminderControls();
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
        PhotoSharedBox.IsEnabled = enabled;
    }

    private void UpdatePhotoDurationLabel()
    {
        var seconds = (int)Math.Round(PhotoDurationSlider.Value);
        PhotoDurationLabel.Text = seconds <= 0 ? "不自动关闭" : $"{seconds} 秒";
    }

    private void UpdateTimerReminderControls()
    {
        TimerReminderCombo.IsEnabled = TimerReminderCheck.IsChecked == true;
    }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        var keyText = (RemoteKeyCombo.Text ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(keyText))
        {
            keyText = "tab";
        }
        if (RemoteEnabledCheck.IsChecked == true)
        {
            if (!KeyBindingParser.TryParse(keyText, out var binding) || binding == null)
            {
                MessageBox.Show("请输入有效的按键组合。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            keyText = binding.ToString();
        }
        RollCallShowId = ShowIdCheck.IsChecked == true;
        RollCallShowName = ShowNameCheck.IsChecked == true;
        RollCallShowPhoto = ShowPhotoCheck.IsChecked == true;
        RollCallPhotoDurationSeconds = (int)Math.Round(PhotoDurationSlider.Value);
        RollCallPhotoSharedClass = (PhotoSharedBox.Text ?? string.Empty).Trim();
        RollCallTimerSoundEnabled = TimerSoundCheck.IsChecked == true;
        RollCallTimerReminderEnabled = TimerReminderCheck.IsChecked == true;
        RollCallTimerReminderIntervalMinutes = ParseReminderMinutes(TimerReminderCombo.Text);
        RollCallRemoteEnabled = RemoteEnabledCheck.IsChecked == true;
        RemotePresenterKey = keyText;
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private static int ParseReminderMinutes(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }
        if (int.TryParse(value.Trim(), out var minutes))
        {
            return Math.Max(0, minutes);
        }
        return 0;
    }
}
