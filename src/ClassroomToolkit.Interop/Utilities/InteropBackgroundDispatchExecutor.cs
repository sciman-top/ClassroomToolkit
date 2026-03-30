using System.Diagnostics;
using System.Threading;

namespace ClassroomToolkit.Interop.Utilities;

internal static class InteropBackgroundDispatchExecutor
{
    internal static void Queue(
        string source,
        Action action,
        Action<Exception>? onError = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        var normalizedSource = string.IsNullOrWhiteSpace(source) ? "unknown" : source.Trim();

        try
        {
            ThreadPool.UnsafeQueueUserWorkItem(
                static state =>
                {
                    var (executorSource, executorAction, executorOnError) = state;
                    try
                    {
                        executorAction();
                    }
                    catch (Exception ex) when (InteropExceptionFilterPolicy.IsNonFatal(ex))
                    {
                        Debug.WriteLine(
                            $"[InteropDispatch][{executorSource}] worker-failed: {ex.GetType().Name} - {ex.Message}");
                        InvokeOnErrorSafely(executorSource, executorOnError, ex, "worker-error-callback");
                    }
                },
                (normalizedSource, action, onError),
                preferLocal: false);
        }
        catch (Exception ex) when (InteropExceptionFilterPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine(
                $"[InteropDispatch][{normalizedSource}] queue-failed: {ex.GetType().Name} - {ex.Message}");
            InvokeOnErrorSafely(normalizedSource, onError, ex, "queue-error-callback");
        }
    }

    private static void InvokeOnErrorSafely(
        string source,
        Action<Exception>? onError,
        Exception error,
        string stage)
    {
        if (onError is null)
        {
            return;
        }

        try
        {
            onError(error);
        }
        catch (Exception callbackEx) when (InteropExceptionFilterPolicy.IsNonFatal(callbackEx))
        {
            Debug.WriteLine(
                $"[InteropDispatch][{source}] {stage}-failed: {callbackEx.GetType().Name} - {callbackEx.Message}");
        }
    }
}
