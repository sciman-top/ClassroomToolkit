using System.IO;
using System.Windows;
using ClassroomToolkit.Services.Presentation;

namespace ClassroomToolkit.App.Paint;

public partial class PaintSettingsDialog
{
    private const string ClassifierPackageFileFilter =
        "规则包 (*.ctpkg.json)|*.ctpkg.json|JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*";

    private string _rollbackPresentationClassifierOverridesJson = string.Empty;
    private bool _hasRollbackPresentationClassifierOverrides;

    private void OnExportClassifierPackageClick(object sender, RoutedEventArgs e)
    {
        if (!PresentationClassifierOverridesPackagePolicy.TryExport(
                _workingPresentationClassifierOverridesJson,
                out var packageJson,
                out var exportError))
        {
            ShowClassifierPackageWarning("导出规则包", $"导出失败：{exportError}");
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "导出演示识别规则包",
            Filter = ClassifierPackageFileFilter,
            FileName = $"presentation-overrides-{DateTime.Now:yyyyMMdd-HHmmss}.ctpkg.json"
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            File.WriteAllText(dialog.FileName, packageJson);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            ShowClassifierPackageWarning("导出规则包", $"写入文件失败：{ex.Message}");
            return;
        }

        RefreshPresentationClassifierPackageStatusText($"规则包状态：已导出 {Path.GetFileName(dialog.FileName)}。");
    }

    private void OnImportClassifierPackageClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "导入演示识别规则包",
            Filter = ClassifierPackageFileFilter
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        string packageJson;
        try
        {
            packageJson = File.ReadAllText(dialog.FileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            ShowClassifierPackageWarning("导入规则包", $"读取文件失败：{ex.Message}");
            return;
        }

        ImportClassifierPackage(
            packageJson,
            sourceTitle: "导入规则包",
            importedStatusPrefix: $"已导入 {Path.GetFileName(dialog.FileName)}");
    }

    private void OnCopyClassifierPackageClick(object sender, RoutedEventArgs e)
    {
        if (!PresentationClassifierOverridesPackagePolicy.TryExport(
                _workingPresentationClassifierOverridesJson,
                out var packageJson,
                out var exportError))
        {
            ShowClassifierPackageWarning("复制规则包", $"复制失败：{exportError}");
            return;
        }

        try
        {
            System.Windows.Clipboard.SetText(packageJson);
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.ExternalException
                                    or InvalidOperationException
                                    or NotSupportedException)
        {
            ShowClassifierPackageWarning("复制规则包", $"写入剪贴板失败：{ex.Message}");
            return;
        }

        RefreshPresentationClassifierPackageStatusText("规则包状态：已复制到剪贴板。");
    }

    private void OnImportClassifierPackageFromClipboardClick(object sender, RoutedEventArgs e)
    {
        string packageJson;
        try
        {
            packageJson = System.Windows.Clipboard.ContainsText() ? System.Windows.Clipboard.GetText() : string.Empty;
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.ExternalException
                                    or InvalidOperationException
                                    or NotSupportedException)
        {
            ShowClassifierPackageWarning("粘贴并导入", $"读取剪贴板失败：{ex.Message}");
            return;
        }

        if (string.IsNullOrWhiteSpace(packageJson))
        {
            ShowClassifierPackageWarning("粘贴并导入", "剪贴板没有可导入的规则包文本。");
            return;
        }

        ImportClassifierPackage(
            packageJson,
            sourceTitle: "粘贴并导入",
            importedStatusPrefix: "已从剪贴板导入");
    }

    private void OnUndoClassifierPackageImportClick(object sender, RoutedEventArgs e)
    {
        if (!_hasRollbackPresentationClassifierOverrides)
        {
            RefreshPresentationClassifierPackageStatusText("规则包状态：没有可撤销的导入。");
            return;
        }

        ApplyWorkingClassifierOverrides(_rollbackPresentationClassifierOverridesJson);
        ClearClassifierImportRollback();
        RefreshPresentationClassifierPackageStatusText(
            BuildClassifierPackageStatusFromOverrides(
                _workingPresentationClassifierOverridesJson,
                importedDetail: "已撤销最近一次导入。"));
        UpdateSectionDirtyStates();
    }

    private void ImportClassifierPackage(
        string packageJson,
        string sourceTitle,
        string importedStatusPrefix)
    {
        if (!PresentationClassifierOverridesPackagePolicy.TryImport(
                packageJson,
                out var importedOverridesJson,
                out var importDetail,
                out var importError))
        {
            ShowClassifierPackageWarning(sourceTitle, $"导入失败：{importError}");
            return;
        }

        var normalizedImported = NormalizePresentationClassifierOverridesJson(importedOverridesJson);
        var confirmationMessage = BuildClassifierImportConfirmationMessage(normalizedImported, importDetail);
        if (System.Windows.MessageBox.Show(
                this,
                confirmationMessage,
                sourceTitle,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            RefreshPresentationClassifierPackageStatusText("规则包状态：已取消导入。");
            return;
        }

        _rollbackPresentationClassifierOverridesJson = _workingPresentationClassifierOverridesJson;
        _hasRollbackPresentationClassifierOverrides = true;
        ApplyWorkingClassifierOverrides(normalizedImported);

        var statusDetail = string.IsNullOrWhiteSpace(importDetail)
            ? $"{importedStatusPrefix}。"
            : $"{importedStatusPrefix}；{importDetail}。";
        RefreshPresentationClassifierPackageStatusText(
            BuildClassifierPackageStatusFromOverrides(
                _workingPresentationClassifierOverridesJson,
                statusDetail));
        UpdateSectionDirtyStates();
    }

    private void ApplyWorkingClassifierOverrides(string? overridesJson)
    {
        _workingPresentationClassifierOverridesJson = NormalizePresentationClassifierOverridesJson(overridesJson);
        PresentationClassifierOverridesJson = _workingPresentationClassifierOverridesJson;
        if (PresentationClassifierClearOverridesCheck.IsChecked == true)
        {
            PresentationClassifierClearOverridesCheck.IsChecked = false;
        }
    }

    private static string BuildClassifierImportConfirmationMessage(string importedOverridesJson, string importDetail)
    {
        var summary = "摘要不可用";
        if (PresentationDiagnosticsProbe.TrySummarizeClassifierOverrides(
                importedOverridesJson,
                out var classTokenCount,
                out var processTokenCount,
                out _))
        {
            summary = $"classToken={classTokenCount}; processToken={processTokenCount}";
        }

        var detailText = string.IsNullOrWhiteSpace(importDetail) ? "未提供额外详情" : importDetail;
        return $"将覆盖当前演示识别规则。\n摘要：{summary}\n详情：{detailText}\n\n是否继续？";
    }

    private void ShowClassifierPackageWarning(string title, string message)
    {
        RefreshPresentationClassifierPackageStatusText($"规则包状态：{message}");
        System.Windows.MessageBox.Show(this, message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void RefreshPresentationClassifierPackageStatusText(string statusText)
    {
        if (PresentationClassifierPackageStatusText == null)
        {
            return;
        }

        PresentationClassifierPackageStatusText.Text = statusText;
        UpdateClassifierPackageActionState();
    }

    private static string BuildClassifierPackageStatusFromOverrides(string? overridesJson, string? importedDetail)
    {
        var normalized = NormalizePresentationClassifierOverridesJson(overridesJson);
        var prefix = string.IsNullOrWhiteSpace(normalized)
            ? "规则包状态：当前未配置自定义覆盖。"
            : "规则包状态：当前已配置自定义覆盖。";

        if (!string.IsNullOrWhiteSpace(normalized)
            && PresentationDiagnosticsProbe.TrySummarizeClassifierOverrides(
                normalized,
                out var classTokenCount,
                out var processTokenCount,
                out _))
        {
            prefix = $"规则包状态：当前已配置自定义覆盖（classToken={classTokenCount}; processToken={processTokenCount}）。";
        }

        return string.IsNullOrWhiteSpace(importedDetail) ? prefix : $"{prefix} {importedDetail}";
    }

    private static string NormalizePresentationClassifierOverridesJson(string? overridesJson)
    {
        return string.IsNullOrWhiteSpace(overridesJson) ? string.Empty : overridesJson.Trim();
    }

    private void UpdateClassifierPackageActionState()
    {
        if (UndoClassifierPackageImportButton == null)
        {
            return;
        }

        UndoClassifierPackageImportButton.IsEnabled = _hasRollbackPresentationClassifierOverrides;
    }

    private void ClearClassifierImportRollback()
    {
        _rollbackPresentationClassifierOverridesJson = string.Empty;
        _hasRollbackPresentationClassifierOverrides = false;
        UpdateClassifierPackageActionState();
    }
}
