using System.Windows;
using ClassroomToolkit.App.Helpers;
using System.Diagnostics;
using System.IO;

namespace ClassroomToolkit.App.Diagnostics;

public partial class StartupCompatibilityWarningDialog : Window
{
    private readonly string? _reportPath;
    private readonly string _diagnosticsPayload;

    public StartupCompatibilityWarningDialog(
        string summary,
        string detail,
        string suggestion,
        string? reportPath = null,
        string? diagnosticsPayload = null)
    {
        InitializeComponent();
        SummaryText.Text = summary;
        DetailBox.Text = detail;
        SuggestionBox.Text = suggestion;
        _reportPath = reportPath;
        _diagnosticsPayload = string.IsNullOrWhiteSpace(diagnosticsPayload)
            ? (string.IsNullOrWhiteSpace(reportPath)
                ? detail
                : $"{detail}{Environment.NewLine}{Environment.NewLine}诊断报告：{reportPath}")
            : diagnosticsPayload;
        OpenReportButton.IsEnabled = !string.IsNullOrWhiteSpace(reportPath) && File.Exists(reportPath);
        CopyDiagnosticsButton.IsEnabled = !string.IsNullOrWhiteSpace(_diagnosticsPayload);
        Loaded += OnDialogLoaded;
        Closed += OnDialogClosed;
    }

    public bool SuppressCurrentIssues => SuppressCheckBox.IsChecked == true;

    private void OnDialogLoaded(object sender, RoutedEventArgs e)
    {
        WindowPlacementHelper.EnsureVisible(this);
    }

    private void OnDialogClosed(object? sender, EventArgs e)
    {
        Loaded -= OnDialogLoaded;
        Closed -= OnDialogClosed;
    }

    private void OnContinueClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnOpenReportClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_reportPath) || !File.Exists(_reportPath))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(_reportPath) { UseShellExecute = true });
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            System.Windows.MessageBox.Show(
                $"打开诊断报告失败：{ex.Message}",
                "启动兼容性提示",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void OnCopyDiagnosticsClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_diagnosticsPayload))
        {
            return;
        }

        try
        {
            System.Windows.Clipboard.SetText(_diagnosticsPayload);
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            System.Windows.MessageBox.Show(
                $"复制诊断信息失败：{ex.Message}",
                "启动兼容性提示",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void OnTitleBarDrag(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
        {
            _ = this.SafeDragMove();
        }
    }
}
