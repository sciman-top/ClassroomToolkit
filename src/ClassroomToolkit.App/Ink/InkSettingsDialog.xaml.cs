using System;
using System.Globalization;
using System.Windows;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.Settings;

namespace ClassroomToolkit.App.Ink;

public partial class InkSettingsDialog : Window
{
    public bool InkRecordEnabled { get; private set; }
    public bool InkReplayPreviousEnabled { get; private set; }
    public int InkRetentionDays { get; private set; }
    public string InkPhotoRootPath { get; private set; } = AppSettings.ResolveDefaultInkPhotoRootPath();

    public InkSettingsDialog(AppSettings settings)
    {
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
        InkRecordEnabled = InkRecordCheck.IsChecked == true;
        InkReplayPreviousEnabled = InkReplayPreviousCheck.IsChecked == true;
        InkRetentionDays = NormalizeRetentionDays(InkRetentionDaysBox.Text);
        InkPhotoRootPath = NormalizePhotoRoot(InkPhotoPathBox.Text);
        DialogResult = true;
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

    private static int NormalizeRetentionDays(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }
        if (!int.TryParse(value, out var days))
        {
            return 0;
        }
        return Math.Max(0, days);
    }
}
