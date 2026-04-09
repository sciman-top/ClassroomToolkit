using System.Windows;
using ClassroomToolkit.App;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.Settings;

namespace ClassroomToolkit.App.Diagnostics;

public partial class DiagnosticsDialog : Window
{
    private readonly DiagnosticsResult _result;
    private readonly AppSettingsService? _settingsService;
    private readonly AppSettings? _settings;

    public DiagnosticsDialog(DiagnosticsResult result, AppSettingsService? settingsService = null, AppSettings? settings = null)
    {
        InitializeComponent();
        _result = result;
        _settingsService = settingsService;
        _settings = settings;
        Title = result.Title;
        SummaryText.Text = result.Summary;
        DetailBox.Text = result.Detail;
        SuggestionBox.Text = string.IsNullOrWhiteSpace(result.Suggestion) ? "暂无建议。" : result.Suggestion;
        
        // 在构造函数中立即修复 BorderBrush 问题
        try
        {
            BorderFixHelper.FixAllBorders(this);
            System.Diagnostics.Debug.WriteLine("DiagnosticsDialog: 构造函数中修复完成");
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            System.Diagnostics.Debug.WriteLine($"DiagnosticsDialog 构造函数修复失败: {ex.Message}");
        }
        
        Loaded += OnDialogLoaded;
        Closed += OnDialogClosed;
    }

    private void OnDialogLoaded(object sender, RoutedEventArgs e)
    {
        WindowPlacementHelper.EnsureVisible(this);

        // 再次诊断 BorderBrush 问题
        try
        {
            BorderBrushDiagnostic.CheckAllBorders(this);
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            System.Diagnostics.Debug.WriteLine($"BorderBrush 诊断失败: {ex.Message}");
        }
    }

    private void OnDialogClosed(object? sender, EventArgs e)
    {
        Loaded -= OnDialogLoaded;
        Closed -= OnDialogClosed;
    }

    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        var text = $"{_result.Title}{Environment.NewLine}{_result.Summary}"
                   + $"{Environment.NewLine}{Environment.NewLine}{_result.Detail}";
        if (!string.IsNullOrWhiteSpace(_result.Suggestion))
        {
            text += $"{Environment.NewLine}{Environment.NewLine}{_result.Suggestion}";
        }
        System.Windows.Clipboard.SetText(text);
    }

    private void OnExportBundleClick(object sender, RoutedEventArgs e)
    {
        var export = DiagnosticsBundleExportService.Export(_result);
        if (export.Success)
        {
            System.Windows.MessageBox.Show(
                this,
                $"诊断包已导出：{export.BundlePath}",
                "导出完成",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        System.Windows.MessageBox.Show(
            this,
            $"导出诊断包失败：{export.Error}",
            "导出失败",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        // 直接关闭窗口，不设置 DialogResult
        // 调用方会通过 SafeShowDialog 的返回值知道结果
        Close();
    }

    private void OnResetStartupWarningsClick(object sender, RoutedEventArgs e)
    {
        if (_settingsService == null || _settings == null)
        {
            System.Windows.MessageBox.Show(
                this,
                "当前窗口未接入设置服务，无法重置启动提示。",
                "提示",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        _settings.StartupCompatibilitySuppressedIssueCodes.Clear();
        _settingsService.Save(_settings);
        System.Windows.MessageBox.Show(
            this,
            "已重新启用启动兼容性提示。下次启动会再次检测。",
            "已恢复",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void OnTitleBarDrag(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
        {
            _ = this.SafeDragMove();
        }
    }
}
