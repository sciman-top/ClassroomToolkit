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

        if (!TryWriteClassifierPackageFile(dialog.FileName, packageJson, out var writeError))
        {
            ShowClassifierPackageWarning("导出规则包", writeError);
            return;
        }

        RefreshPresentationClassifierPackageStatusText($"规则包状态：已导出 {GetClassifierPackageFileName(dialog.FileName)}。");
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

        if (!TryReadClassifierPackageFile(dialog.FileName, out var packageJson, out var readError))
        {
            ShowClassifierPackageWarning("导入规则包", readError);
            return;
        }

        ImportClassifierPackage(
            packageJson,
            sourceTitle: "导入规则包",
            importedStatusPrefix: $"已导入 {GetClassifierPackageFileName(dialog.FileName)}");
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
}
