using System;
using System.Globalization;
using System.Windows;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.Settings;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App.Ink;

public partial class InkSettingsDialog : Window
{
    public bool InkRecordEnabled { get; private set; }
    public bool InkReplayPreviousEnabled { get; private set; }
    public int InkRetentionDays { get; private set; }
    public string InkPhotoRootPath { get; private set; } = AppSettings.ResolveDefaultInkPhotoRootPath();

    public InkSettingsDialog(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        InitializeComponent();
        InkRecordCheck.IsChecked = settings.InkRecordEnabled;
        InkReplayPreviousCheck.IsChecked = settings.InkReplayPreviousEnabled;
        InkRetentionDaysBox.Text = settings.InkRetentionDays.ToString(CultureInfo.InvariantCulture);
        InkPhotoPathBox.Text = settings.InkPhotoRootPath;
        InkRecordCheck.Checked += OnInkRecordToggleChanged;
        InkRecordCheck.Unchecked += OnInkRecordToggleChanged;
        Closed += OnDialogClosed;
        UpdateInkRecordState();
    }

    private void OnInkRecordToggleChanged(object? sender, RoutedEventArgs e)
    {
        UpdateInkRecordState();
    }

    private void OnDialogClosed(object? sender, EventArgs e)
    {
        InkRecordCheck.Checked -= OnInkRecordToggleChanged;
        InkRecordCheck.Unchecked -= OnInkRecordToggleChanged;
        Closed -= OnDialogClosed;
    }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        if (!TryNormalizeRetentionDays(InkRetentionDaysBox.Text, out var retentionDays))
        {
            TopmostMessageBox.Show(
                this,
                "请输入不小于 0 的整数天数。",
                "提示",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        InkRecordEnabled = InkRecordCheck.IsChecked == true;
        InkReplayPreviousEnabled = InkReplayPreviousCheck.IsChecked == true;
        InkRetentionDays = retentionDays;
        InkPhotoRootPath = NormalizePhotoRoot(InkPhotoPathBox.Text);
        DialogResult = true;
    }

    private void OnRestoreDefaultsClick(object sender, RoutedEventArgs e)
    {
        ApplyDefaultSettings();
    }

    private void ApplyDefaultSettings()
    {
        var defaults = new AppSettings();
        InkRecordCheck.IsChecked = defaults.InkRecordEnabled;
        InkReplayPreviousCheck.IsChecked = defaults.InkReplayPreviousEnabled;
        InkRetentionDaysBox.Text = defaults.InkRetentionDays.ToString(CultureInfo.InvariantCulture);
        InkPhotoPathBox.Text = defaults.InkPhotoRootPath;
        UpdateInkRecordState();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void UpdateInkRecordState()
    {
        bool enabled = InkRecordCheck.IsChecked == true;
        InkReplayPreviousCheck.IsEnabled = enabled;
        InkRetentionDaysBox.IsEnabled = enabled;
        InkPhotoPathBox.IsEnabled = enabled;
        if (!enabled)
        {
            InkReplayPreviousCheck.IsChecked = false;
        }
    }

    private void OnTitleBarDrag(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
        {
            _ = this.SafeDragMove();
        }
    }

    private static string NormalizePhotoRoot(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return AppSettings.ResolveDefaultInkPhotoRootPath();
        }
        return value.Trim();
    }

    internal static bool TryNormalizeRetentionDays(string? value, out int days)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out days)
            || days < 0)
        {
            days = 0;
            return false;
        }

        return true;
    }
}
