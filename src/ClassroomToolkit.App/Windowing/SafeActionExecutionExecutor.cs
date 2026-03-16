namespace ClassroomToolkit.App.Windowing;

internal static class SafeActionExecutionExecutor
{
    internal static bool TryExecute(Action action, Action<Exception>? onFailure = null)
    {
        ArgumentNullException.ThrowIfNull(action);

        try
        {
            action();
            return true;
        }
        catch (Exception ex) when (WindowingExceptionFilterPolicy.IsNonFatal(ex))
        {
            if (onFailure != null)
            {
                try
                {
                    onFailure(ex);
                }
                catch (Exception callbackEx) when (WindowingExceptionFilterPolicy.IsNonFatal(callbackEx))
                {
                }
            }
            return false;
        }
    }
}
