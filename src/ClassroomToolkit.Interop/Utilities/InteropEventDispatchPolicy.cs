using System.Diagnostics;

namespace ClassroomToolkit.Interop.Utilities;

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
