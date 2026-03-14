namespace ClassroomToolkit.App.Windowing;

internal static class ToolbarInteractionRetouchStateUpdater
{
    internal static void MarkPreviewMouseDown(
        ref ToolbarInteractionRetouchRuntimeState state,
        DateTime nowUtc)
    {
        state = state with
        {
            LastPreviewMouseDownUtc = nowUtc
        };
    }

    internal static void MarkRetouched(
        ref ToolbarInteractionRetouchRuntimeState state,
        DateTime nowUtc)
    {
        state = state with
        {
            LastRetouchUtc = nowUtc
        };
    }

    internal static void Reset(ref ToolbarInteractionRetouchRuntimeState state)
    {
        state = ToolbarInteractionRetouchRuntimeState.Default;
    }
}
