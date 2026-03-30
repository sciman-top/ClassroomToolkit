namespace ClassroomToolkit.App.Paint;

internal static class CrossPageUpdateReplayPolicy
{
    internal static bool IsReplayBaseSource(string source)
    {
        return string.Equals(source, CrossPageUpdateSources.InkVisualSyncReplay, StringComparison.Ordinal)
            || string.Equals(source, CrossPageUpdateSources.InteractionReplay, StringComparison.Ordinal);
    }

    internal static bool ShouldQueueReplay(CrossPageUpdateSourceKind kind)
    {
        return kind is CrossPageUpdateSourceKind.VisualSync or CrossPageUpdateSourceKind.Interaction;
    }

    internal static bool ShouldFlushReplay(
        bool replayPending,
        bool crossPageUpdatePending,
        bool photoModeActive,
        bool crossPageDisplayEnabled,
        bool interactionActive)
    {
        return replayPending
            && !crossPageUpdatePending
            && photoModeActive
            && crossPageDisplayEnabled
            && !interactionActive;
    }
}
