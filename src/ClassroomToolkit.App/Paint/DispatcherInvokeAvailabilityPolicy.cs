namespace ClassroomToolkit.App.Paint;

internal static class DispatcherInvokeAvailabilityPolicy
{
    internal static bool CanBeginInvoke(bool hasShutdownStarted, bool hasShutdownFinished)
    {
        return !hasShutdownStarted && !hasShutdownFinished;
    }
}
