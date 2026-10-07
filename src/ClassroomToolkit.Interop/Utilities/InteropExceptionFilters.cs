using
System.Diagnostics;
using
System.Threading;

namespace ClassroomToolkit.Interop.Utilities;

internal static class InteropExceptionFilterPolicy
{
    internal static bool IsNonFatal(Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);

        return ex is not (
            OutOfMemoryException
            or AppDomainUnloadedException
            or BadImageFormatException
            or CannotUnloadAppDomainException
            or InvalidProgramException
            or StackOverflowException
            or AccessViolationException);
    }
}

internal static class InteropHookDiagnostics
{
    internal static void LogSlowCallback(string component, long startTimestamp, int timeoutMs)
    {
        var elapsedMs = (Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency;
        if (elapsedMs <= timeoutMs)
        {
            return;
        }

        Debug.WriteLine($"[{component}] Callback took {elapsedMs:F1}ms");
    }

    internal static void RecordCallbackException(
        string component,
        string source,
        Exception ex,
        ref int callbackExceptionCount,
        ref long lastExceptionLogTick,
        int exceptionLogIntervalMs)
    {
        var count = Interlocked.Increment(ref callbackExceptionCount);
        var now = Environment.TickCount64;
        var last = Interlocked.Read(ref lastExceptionLogTick);
        if (now - last < exceptionLogIntervalMs)
        {
            return;
        }

        Interlocked.Exchange(ref lastExceptionLogTick, now);
        Debug.WriteLine($"[{component}][{source}] callback exception count={count}, type={ex.GetType().Name}, message={ex.Message}");
    }
}

internal static class InteropEventDispatchPolicy
{
    internal static void InvokeSafely<T>(
        Action<T>? handlers,
        T arg,
        string source)
    {
        if (handlers is null)
        {
            return;
        }

        var invocationList = handlers.GetInvocationList();
        foreach (var callback in invocationList)
        {
            try
            {
                ((Action<T>)callback)(arg);
            }
            catch (Exception ex) when (InteropExceptionFilterPolicy.IsNonFatal(ex))
            {
                Debug.WriteLine(
                    $"[InteropEventDispatch][{source}] subscriber-failed: {ex.GetType().Name} - {ex.Message}");
            }
        }
    }

    internal static void InvokeSafely<T1, T2>(
        Action<T1, T2>? handlers,
        T1 arg1,
        T2 arg2,
        string source)
    {
        if (handlers is null)
        {
            return;
        }

        var invocationList = handlers.GetInvocationList();
        foreach (var callback in invocationList)
        {
            try
            {
                ((Action<T1, T2>)callback)(arg1, arg2);
            }
            catch (Exception ex) when (InteropExceptionFilterPolicy.IsNonFatal(ex))
            {
                Debug.WriteLine(
                    $"[InteropEventDispatch][{source}] subscriber-failed: {ex.GetType().Name} - {ex.Message}");
            }
        }
    }
}
