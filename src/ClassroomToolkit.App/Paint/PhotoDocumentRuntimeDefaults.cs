namespace ClassroomToolkit.App.Paint;

internal static class PhotoDocumentRuntimeDefaults
{
    internal const double PdfDefaultDpi = 96;
    internal const int PdfCacheLimit = 6;
    internal const long PdfCacheMaxBytes = 100L * 1024L * 1024L;
    internal const int PdfCacheTryEnterTimeoutMs = 50;
    internal const int PdfPrefetchTryEnterTimeoutMs = 100;
    internal const int PdfPrefetchDelayMs = 120;
    internal const int NeighborPageCacheLimit = 5;
}
