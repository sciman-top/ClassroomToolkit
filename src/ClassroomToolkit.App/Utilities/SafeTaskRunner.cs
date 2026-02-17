using System.Diagnostics;
using System.Threading;

namespace ClassroomToolkit.App.Utilities;

internal static class SafeTaskRunner
{
    public static Task Run(
        string source,
        Action<CancellationToken> action,
        CancellationToken cancellationToken = default,
        Action<Exception>? onError = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        return Run(
            source,
            token =>
            {
                action(token);
                return Task.CompletedTask;
            },
            cancellationToken,
            onError);
    }

    public static Task Run(
        string source,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default,
        Action<Exception>? onError = null)
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
            catch (Exception ex)
            {
                Debug.WriteLine($"[SafeTaskRunner][{normalizedSource}] failed: {ex.GetType().Name} - {ex.Message}");
                onError?.Invoke(ex);
            }
        }, CancellationToken.None);
    }
}
