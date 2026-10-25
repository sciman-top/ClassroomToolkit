using System.Windows;
using ClassroomToolkit.Services.Presentation;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App.Paint;

public partial class PaintSettingsDialog
{
    private void ShowClassifierPackageWarning(string title, string message)
    {
        RefreshPresentationClassifierPackageStatusText($"规则包状态：{message}");
        TopmostMessageBox.Show(this, message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
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
