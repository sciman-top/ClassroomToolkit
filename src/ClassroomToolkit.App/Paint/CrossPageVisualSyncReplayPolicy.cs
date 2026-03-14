namespace ClassroomToolkit.App.Paint;

internal static class CrossPageVisualSyncReplayPolicy
{
    internal static bool IsVisualSyncSource(string source)
    {
        return CrossPageUpdateSourceClassifier.Classify(source) == CrossPageUpdateSourceKind.VisualSync;
    }

    internal static bool ShouldQueueReplay(bool crossPageUpdatePending, string source)
    {
        return crossPageUpdatePending && IsVisualSyncSource(source);
    }

    internal static bool ShouldFlushReplay(
        bool replayPending,
        bool crossPageUpdatePending,
        bool photoModeActive,
        bool crossPageDisplayEnabled,
        bool interactionActive)
    {
        return CrossPageUpdateReplayPolicy.ShouldFlushReplay(
            replayPending,
            crossPageUpdatePending,
            photoModeActive,
            crossPageDisplayEnabled,
            interactionActive);
    }
}
