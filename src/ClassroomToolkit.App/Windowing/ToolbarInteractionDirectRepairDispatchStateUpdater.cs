namespace ClassroomToolkit.App.Windowing;

internal static class ToolbarInteractionDirectRepairDispatchStateUpdater
{
    internal static bool TryMarkQueued(ref bool queued)
    {
        if (queued)
        {
            return false;
        }

        queued = true;
        return true;
    }

    internal static void Clear(ref bool queued)
    {
        queued = false;
    }
}
