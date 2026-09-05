using System.Windows;
using ClassroomToolkit.App;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.Settings;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App.Diagnostics;

public partial class DiagnosticsDialog : Window
{
    private readonly DiagnosticsResult _result;
    private readonly AppSettingsService? _settingsService;
    private readonly AppSettings? _settings;

    public DiagnosticsDialog(DiagnosticsResult result, AppSettingsService? settingsService = null, AppSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(result);

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
        try
        {
            System.Windows.Clipboard.SetText(text);
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            // 剪贴板常被远控/输入法/剪贴板工具占用，降级提示而不是抛进全局错误弹窗。
            TopmostMessageBox.Show(
                this,
                "复制失败：剪贴板被其他程序占用，请稍后重试。",
                "提示",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void OnExportBundleClick(object sender, RoutedEventArgs e)
    {
        var export = DiagnosticsBundleExportService.Export(_result);
        if (export.Success)
        {
            TopmostMessageBox.Show(
                this,
                $"诊断包已导出：{export.BundlePath}",
                "导出完成",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        TopmostMessageBox.Show(
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
            TopmostMessageBox.Show(
                this,
                "当前窗口未接入设置服务，无法重置启动提示。",
                "提示",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        _settings.StartupCompatibilitySuppressedIssueCodes.Clear();
        try
        {
            _settingsService.Save(_settings);
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            TopmostMessageBox.Show(
                this,
                $"保存设置失败：{ex.Message}\n请检查设置文件权限或磁盘状态。",
                "提示",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }
        TopmostMessageBox.Show(
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
