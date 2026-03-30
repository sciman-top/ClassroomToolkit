namespace ClassroomToolkit.App.Windowing;

internal static class ExplicitForegroundRetouchStateUpdater
{
    internal static void MarkRetouched(
        ref ExplicitForegroundRetouchRuntimeState state,
        DateTime nowUtc)
    {
        state = new ExplicitForegroundRetouchRuntimeState(nowUtc);
    }
}
