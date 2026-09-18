namespace ClassroomToolkit.App;

internal sealed class ApplicationExitCoordinator
{
    private bool _inProgress;

    internal bool TryExit(
        Func<bool> saveSettings,
        Func<bool> confirmDiscardSettings,
        Func<bool, bool> prepareChildWindows,
        Action shutdown)
    {
        // Save-error dialogs pump the dispatcher; a second exit must not begin
        // cancelling resources while the first request still awaits a decision.
        if (_inProgress)
        {
            return false;
        }

        _inProgress = true;
        try
        {
            var discardSettings = !saveSettings();
            if (discardSettings && !confirmDiscardSettings())
            {
                return false;
            }
            if (!prepareChildWindows(discardSettings))
            {
                return false;
            }

            shutdown();
            return true;
        }
        finally
        {
            _inProgress = false;
        }
    }
}
