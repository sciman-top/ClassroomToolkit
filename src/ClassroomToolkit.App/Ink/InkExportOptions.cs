namespace ClassroomToolkit.App.Ink;

public enum InkExportScope
{
    AllPersistedAndSession = 0,
    SessionChangesOnly = 1
}

/// <summary>
/// Options controlling how ink annotations are exported as composite images.
/// </summary>
public sealed class InkExportOptions
{
    /// <summary>
    /// DPI used when rendering PDF pages to bitmap before compositing.
    /// Default: 150.
    /// </summary>
    public int Dpi { get; set; } = 150;

    /// <summary>
    /// Output image format. Supported: "PNG", "JPG".
    /// For single-image sources, the original format is preserved by default.
    /// </summary>
    public string Format { get; set; } = "PNG";

    /// <summary>
    /// JPEG quality (1-100). Only used when Format is "JPG".
    /// </summary>
    public int JpegQuality { get; set; } = 90;

    /// <summary>
    /// Export scope. Default exports all persisted and in-memory ink.
    /// </summary>
    public InkExportScope Scope { get; set; } = InkExportScope.AllPersistedAndSession;

    /// <summary>
    /// Maximum number of files exported concurrently in batch mode.
    /// Set to 0 or negative to use adaptive concurrency.
    /// </summary>
    public int MaxParallelFiles { get; set; } = 2;
}
