namespace ClassroomToolkit.App.Windowing;

internal static class ZOrderApplyGuardStateUpdater
{
    internal static bool TryEnter(ref bool applying)
    {
        if (applying)
        {
            return false;
        }

        applying = true;
        return true;
    }

    internal static void Exit(ref bool applying)
    {
        applying = false;
    }
}
