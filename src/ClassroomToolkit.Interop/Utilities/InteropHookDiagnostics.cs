using System.Diagnostics;
using System.Threading;

namespace ClassroomToolkit.Interop.Utilities;

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
