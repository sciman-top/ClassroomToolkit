namespace ClassroomToolkit.App.Windowing;

internal static class ToolbarInteractionRetouchRuntimeResetExecutor
{
    internal static void Apply(
        ref bool directRepairBackgroundQueued,
        ref bool directRepairRerunRequested,
        ref ToolbarInteractionRetouchRuntimeState retouchState)
    {
        ToolbarInteractionDirectRepairDispatchStateUpdater.Clear(ref directRepairBackgroundQueued);
        ToolbarInteractionDirectRepairRerunStateUpdater.Clear(ref directRepairRerunRequested);
        ToolbarInteractionRetouchStateUpdater.Reset(ref retouchState);
    }
}
