namespace ClassroomToolkit.App.Windowing;

internal static class OverlayActivatedRetouchStateUpdater
{
    internal static void MarkSuppressNextApply(ref OverlayActivatedRetouchRuntimeState state)
    {
        state = state with { SuppressNextApply = true };
    }

    internal static bool TryConsumeSuppression(ref OverlayActivatedRetouchRuntimeState state)
    {
        if (!state.SuppressNextApply)
        {
            return false;
        }

        state = state with { SuppressNextApply = false };
        return true;
    }

    internal static void MarkRetouched(ref OverlayActivatedRetouchRuntimeState state, DateTime nowUtc)
    {
        state = state with { LastRetouchUtc = nowUtc };
    }
}
