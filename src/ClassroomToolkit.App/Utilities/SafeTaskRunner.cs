using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using ClassroomToolkit.App;

namespace ClassroomToolkit.App.Utilities;

internal static class SafeTaskRunner
{
    public static Task Run(
        string source,
        Action<CancellationToken> action,
        Action<Exception>? onError = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        return Run(
            source,
            token =>
            {
                action(token);
                return Task.CompletedTask;
            },
            onError,
            cancellationToken);
    }

    [SuppressMessage(
        "Design",
        "CA1068:CancellationToken parameters must come last",
        Justification = "Legacy parameter order kept for backward compatibility with existing call sites.")]
    public static Task Run(
        string source,
        Action<CancellationToken> action,
        CancellationToken cancellationToken = default,
        Action<Exception>? onError = null)
    {
        return Run(source, action, onError, cancellationToken);
    }

    public static Task Run(
        string source,
        Func<CancellationToken, Task> action,
        Action<Exception>? onError = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        var normalizedSource = string.IsNullOrWhiteSpace(source) ? "unknown" : source.Trim();
        if (cancellationToken.IsCancellationRequested)
        {
            Debug.WriteLine($"[SafeTaskRunner][{normalizedSource}] canceled before scheduling.");
            return Task.CompletedTask;
        }

        return Task.Run(async () =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await action(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Debug.WriteLine($"[SafeTaskRunner][{normalizedSource}] canceled.");
            }
            catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
            {
                Debug.WriteLine($"[SafeTaskRunner][{normalizedSource}] failed: {ex.GetType().Name} - {ex.Message}");
                if (onError != null)
                {
                    try
                    {
                        onError(ex);
                    }
                    catch (Exception callbackEx) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(callbackEx))
                    {
                        Debug.WriteLine(
                            $"[SafeTaskRunner][{normalizedSource}] onError failed: {callbackEx.GetType().Name} - {callbackEx.Message}");
                    }
                }
            }
        }, cancellationToken);
    }

    [SuppressMessage(
        "Design",
        "CA1068:CancellationToken parameters must come last",
        Justification = "Legacy parameter order kept for backward compatibility with existing call sites.")]
    public static Task Run(
        string source,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default,
        Action<Exception>? onError = null)
    {
        return Run(source, action, onError, cancellationToken);
    }
}
