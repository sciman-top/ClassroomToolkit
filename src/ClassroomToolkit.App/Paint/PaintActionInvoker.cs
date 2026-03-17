using System;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App.Paint;

internal static class PaintActionInvoker
{
    internal static void TryInvoke(Action action)
    {
        _ = SafeActionExecutionExecutor.TryExecute(action);
    }

    internal static TResult TryInvoke<TResult>(
        Func<TResult> action,
        TResult fallback = default!,
        Action<Exception>? onFailure = null)
    {
        return SafeActionExecutionExecutor.TryExecute(action, fallback, onFailure);
    }
}
