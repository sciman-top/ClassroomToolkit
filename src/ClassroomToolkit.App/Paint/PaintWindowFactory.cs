using System;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Settings;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace ClassroomToolkit.App.Paint;

public sealed class PaintWindowFactory : IPaintWindowFactory
{
    private readonly InkPersistenceService _persistence;
    private readonly InkExportService _export;
    private readonly InkExportOptions _exportOptions;
    private readonly ILogger<PaintOverlayWindow> _overlayLogger;
    private readonly bool _useInkHistorySqlite;

    public PaintWindowFactory(
        InkPersistenceService persistence,
        InkExportService export,
        InkExportOptions exportOptions,
        ILogger<PaintOverlayWindow> overlayLogger)
    {
        ArgumentNullException.ThrowIfNull(persistence);
        ArgumentNullException.ThrowIfNull(export);
        ArgumentNullException.ThrowIfNull(exportOptions);
        ArgumentNullException.ThrowIfNull(overlayLogger);

        _persistence = persistence;
        _export = export;
        _exportOptions = exportOptions;
        _overlayLogger = overlayLogger;
        _useInkHistorySqlite = AppFlags.UseSqliteBusinessStore
            && ClassroomToolkit.Infra.Storage.BusinessStorageBackendCapabilityPolicy.IsSqliteAvailable(AppFlags.EnableExperimentalSqliteBackend);
    }

    public (PaintOverlayWindow overlay, PaintToolbarWindow toolbar) Create()
    {
        var overlay = new PaintOverlayWindow(_overlayLogger);
        ClassroomToolkit.Infra.Storage.InkHistorySqliteStoreAdapter? historyAdapter = null;
        if (_useInkHistorySqlite)
        {
            historyAdapter = new ClassroomToolkit.Infra.Storage.InkHistorySqliteStoreAdapter(new InkHistoryPersistenceBridge(_persistence));
        }

        Debug.WriteLine(
            $"[Storage] InkHistory backend selected={(_useInkHistorySqlite ? "Sqlite" : "Sidecar")}, preferSqlite={AppFlags.UseSqliteBusinessStore}, experimentalSqlite={AppFlags.EnableExperimentalSqliteBackend}");
        overlay.SetInkPersistenceServices(_persistence, _export, _exportOptions, historyAdapter);
        return (overlay, new PaintToolbarWindow());
    }
}
