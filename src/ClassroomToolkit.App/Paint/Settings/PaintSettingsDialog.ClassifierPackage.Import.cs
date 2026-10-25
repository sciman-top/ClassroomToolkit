using System.IO;
using System.Windows;
using ClassroomToolkit.Services.Presentation;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App.Paint;

public partial class PaintSettingsDialog
{
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
        if (TopmostMessageBox.Show(
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

    private static bool TryReadClassifierPackageFile(string fileName, out string packageJson, out string readError)
    {
        try
        {
            packageJson = File.ReadAllText(fileName);
            readError = string.Empty;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            packageJson = string.Empty;
            readError = $"读取文件失败：{ex.Message}";
            return false;
        }
    }

    private static bool TryWriteClassifierPackageFile(string fileName, string packageJson, out string writeError)
    {
        try
        {
            File.WriteAllText(fileName, packageJson);
            writeError = string.Empty;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            writeError = $"写入文件失败：{ex.Message}";
            return false;
        }
    }

    private static string GetClassifierPackageFileName(string fileName)
    {
        return Path.GetFileName(fileName);
    }
}
