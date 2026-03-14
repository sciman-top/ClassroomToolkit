namespace ClassroomToolkit.App.Paint;

internal static class PdfPrefetchTimingPolicy
{
    internal static int ResolveInitialDelayMs(bool crossPageDisplayEnabled)
    {
        return crossPageDisplayEnabled ? 0 : PhotoDocumentRuntimeDefaults.PdfPrefetchDelayMs;
    }
}
