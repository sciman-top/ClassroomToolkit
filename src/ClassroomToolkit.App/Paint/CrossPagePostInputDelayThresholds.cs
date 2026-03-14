namespace ClassroomToolkit.App.Paint;

internal static class CrossPagePostInputDelayThresholds
{
    internal const int FallbackDelayMs = CrossPageRuntimeDefaults.PostInputRefreshDelayMs;
    internal const int NeighborRenderMinMs = 180;
    internal const int NeighborMissingMinMs = 200;
    internal const int ReplayMinMs = 220;
}
