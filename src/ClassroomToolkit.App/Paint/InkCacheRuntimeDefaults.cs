namespace ClassroomToolkit.App.Paint;

internal static class InkCacheRuntimeDefaults
{
    internal const int HistoryLimit = 20;
    internal const long MaxHistoryMemoryBytes = 512L * 1024L * 1024L;
    internal const int NoiseTileCacheLimit = 96;
    internal const int SolidBrushCacheLimit = 256;
    internal const int PenCacheLimit = 192;
}
