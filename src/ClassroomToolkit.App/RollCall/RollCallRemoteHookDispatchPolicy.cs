namespace ClassroomToolkit.App.RollCall;

internal static class RollCallRemoteHookDispatchPolicy
{
    internal static bool CanDispatch(
        bool dispatcherShutdownStarted,
        bool dispatcherShutdownFinished)
    {
        return !dispatcherShutdownStarted && !dispatcherShutdownFinished;
    }
}
