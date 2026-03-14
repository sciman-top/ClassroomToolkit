namespace ClassroomToolkit.App.Windowing;

internal static class ToolbarInteractionDirectRepairRerunStateUpdater
{
    internal static void Request(ref bool rerunRequested)
    {
        rerunRequested = true;
    }

    internal static bool TryConsume(ref bool rerunRequested)
    {
        if (!rerunRequested)
        {
            return false;
        }

        rerunRequested = false;
        return true;
    }

    internal static void Clear(ref bool rerunRequested)
    {
        rerunRequested = false;
    }
}
