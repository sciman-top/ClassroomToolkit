using System;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.Application.Abstractions;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace ClassroomToolkit.App.Paint;

public sealed class PaintWindowFactory : IPaintWindowFactory
{
    private readonly InkPersistenceService _persistence;
    private readonly InkExportService _export;
    private readonly InkExportOptions _exportOptions;
    private readonly ILogger<PaintOverlayWindow> _overlayLogger;
    private readonly IInkHistorySnapshotStore? _inkHistorySnapshotStore;

    public PaintWindowFactory(
        InkPersistenceService persistence,
        InkExportService export,
        InkExportOptions exportOptions,
        ILogger<PaintOverlayWindow> overlayLogger,
        IInkHistorySnapshotStore? inkHistorySnapshotStore = null)
    {
        ArgumentNullException.ThrowIfNull(persistence);
        ArgumentNullException.ThrowIfNull(export);
        ArgumentNullException.ThrowIfNull(exportOptions);
        ArgumentNullException.ThrowIfNull(overlayLogger);

        _persistence = persistence;
        _export = export;
        _exportOptions = exportOptions;
        _overlayLogger = overlayLogger;
        _inkHistorySnapshotStore = inkHistorySnapshotStore;
    }

    public (PaintOverlayWindow overlay, PaintToolbarWindow toolbar) Create()
    {
        var overlay = new PaintOverlayWindow(_overlayLogger);
        Debug.WriteLine(
            $"[Storage] InkHistory backend selected={(_inkHistorySnapshotStore != null ? "Sqlite" : "Sidecar")}");
        overlay.SetInkPersistenceServices(_persistence, _export, _exportOptions, _inkHistorySnapshotStore);
        return (overlay, new PaintToolbarWindow());
    }
}
