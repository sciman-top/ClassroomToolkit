using System;
using System.IO;
using System.Windows;
using ClassroomToolkit.Application.Abstractions;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Windowing;
using MessageBox = System.Windows.MessageBox;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private void ShowExportMessageSafe(
        string operation,
        string message,
        string title,
        MessageBoxImage image)
    {
        SafeActionExecutionExecutor.TryExecute(
            () => MessageBox.Show(this, message, title, MessageBoxButton.OK, image),
            ex => System.Diagnostics.Debug.WriteLine(
                $"[InkExport][{operation}] messagebox failed: {ex.GetType().Name} - {ex.Message}"));
    }

    private MessageBoxResult ShowExportConfirmationSafe(
        string operation,
        string message,
        string title,
        MessageBoxButton buttons,
        MessageBoxImage image,
        MessageBoxResult fallback = MessageBoxResult.Cancel)
    {
        var result = fallback;
        SafeActionExecutionExecutor.TryExecute(
            () => result = MessageBox.Show(this, message, title, buttons, image),
            ex => System.Diagnostics.Debug.WriteLine(
                $"[InkExport][{operation}] confirm failed: {ex.GetType().Name} - {ex.Message}"));
        return result;
    }

    private void DispatchExportUiUpdate(string operation, Action action)
    {
        var scheduled = TryBeginInvoke(action, System.Windows.Threading.DispatcherPriority.Background);
        if (scheduled)
        {
            return;
        }

        if (Dispatcher.CheckAccess())
        {
            SafeActionExecutionExecutor.TryExecute(
                action,
                ex => System.Diagnostics.Debug.WriteLine(
                    $"[InkExport][{operation}] inline-ui-update failed: {ex.GetType().Name} - {ex.Message}"));
            return;
        }

        System.Diagnostics.Debug.WriteLine($"[InkExport][{operation}] ui-update dispatch failed");
    }

    /// <summary>
    /// Inject the persistence and export services. Must be called after construction.
    /// </summary>
    public void SetInkPersistenceServices(
        InkPersistenceService persistence,
        InkExportService export,
        InkExportOptions? exportOptions = null,
        IInkHistorySnapshotStore? inkHistorySnapshotStore = null)
    {
        ArgumentNullException.ThrowIfNull(persistence);
        ArgumentNullException.ThrowIfNull(export);

        _inkPersistence = persistence;
        _inkHistorySnapshotStore = inkHistorySnapshotStore;
        _inkExport = export;
        if (exportOptions != null)
        {
            _inkExportOptions = exportOptions;
        }
    }

    public void SetSessionCaptureExportDirectory(string? directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            _sessionCaptureExportDirectory = string.Empty;
            return;
        }

        try
        {
            var fullPath = Path.GetFullPath(directoryPath);
            _sessionCaptureExportDirectory = Directory.Exists(fullPath)
                ? fullPath
                : string.Empty;
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            _sessionCaptureExportDirectory = string.Empty;
            System.Diagnostics.Debug.WriteLine($"[InkExport] invalid session capture export directory: {ex.Message}");
        }
    }
}
