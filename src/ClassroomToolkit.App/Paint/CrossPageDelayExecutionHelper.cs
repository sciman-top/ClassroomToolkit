using System;
using System.Threading.Tasks;
using ClassroomToolkit.App;

namespace ClassroomToolkit.App.Paint;

internal static class CrossPageDelayExecutionHelper
{
    internal static async Task<(bool Success, string? FailureDetail)> TryDelayAsync(
        int delayMs,
        Func<int, Task> delayAsync)
    {
        try
        {
            await delayAsync(delayMs).ConfigureAwait(false);
            return (Success: true, FailureDetail: null);
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            return (
                Success: false,
                FailureDetail: CrossPageDelayedDispatchFailureDiagnosticsPolicy.FormatDelayFailureDetail(
                    ex.GetType().Name));
        }
    }
}
