namespace ClassroomToolkit.App.Windowing;

internal static class SessionTransitionDuplicateStateUpdater
{
    internal static void Reset(ref long lastAppliedTransitionId)
    {
        lastAppliedTransitionId = 0;
    }

    internal static void MarkApplied(
        ref long lastAppliedTransitionId,
        long currentTransitionId)
    {
        if (currentTransitionId > lastAppliedTransitionId)
        {
            lastAppliedTransitionId = currentTransitionId;
        }
    }
}
