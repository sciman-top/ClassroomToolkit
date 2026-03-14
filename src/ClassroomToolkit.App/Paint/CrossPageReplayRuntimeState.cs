namespace ClassroomToolkit.App.Paint;

internal readonly record struct CrossPageReplayRuntimeState(
    bool VisualSyncReplayPending,
    bool InteractionReplayPending,
    bool PreferInteractionReplay,
    CrossPageReplayDispatchTarget LastDispatchTarget)
{
    internal static CrossPageReplayRuntimeState Default => new(
        VisualSyncReplayPending: false,
        InteractionReplayPending: false,
        PreferInteractionReplay: false,
        LastDispatchTarget: CrossPageReplayDispatchTarget.None);
}
