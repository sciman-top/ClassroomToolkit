namespace ClassroomToolkit.App.Paint;

internal static class CrossPageReplayDispatchRequestPolicy
{
    internal static string? ResolveSource(CrossPageReplayDispatchTarget target)
    {
        return target switch
        {
            CrossPageReplayDispatchTarget.VisualSync => CrossPageUpdateSources.InkVisualSyncReplay,
            CrossPageReplayDispatchTarget.Interaction => CrossPageUpdateSources.InteractionReplay,
            _ => null
        };
    }
}
