using ClassroomToolkit.App.Ink;

namespace ClassroomToolkit.App.Paint;

public sealed class PaintWindowFactory : IPaintWindowFactory
{
    private readonly InkPersistenceService _persistence;
    private readonly InkExportService _export;
    private readonly InkExportOptions _exportOptions;

    public PaintWindowFactory(InkPersistenceService persistence, InkExportService export, InkExportOptions exportOptions)
    {
        _persistence = persistence;
        _export = export;
        _exportOptions = exportOptions;
    }

    public (PaintOverlayWindow overlay, PaintToolbarWindow toolbar) Create()
    {
        var overlay = new PaintOverlayWindow();
        overlay.SetInkPersistenceServices(_persistence, _export, _exportOptions);
        return (overlay, new PaintToolbarWindow());
    }
}
