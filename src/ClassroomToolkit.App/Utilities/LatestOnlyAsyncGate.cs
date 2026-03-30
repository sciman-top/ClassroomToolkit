using System.Threading;

namespace ClassroomToolkit.App.Utilities;

internal sealed class LatestOnlyAsyncGate : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private int _generation;
    private int _disposed;

    public int NextGeneration()
    {
        return Interlocked.Increment(ref _generation);
    }

    public bool IsCurrent(int generation)
    {
        return Volatile.Read(ref _generation) == generation;
    }

    public async Task RunAsync(int generation, Func<Func<bool>, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        var entered = false;
        try
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            entered = true;
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        try
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                return;
            }
            if (!IsCurrent(generation))
            {
                return;
            }
            await action(() => IsCurrent(generation)).ConfigureAwait(false);
        }
        finally
        {
            if (entered)
            {
                try
                {
                    _gate.Release();
                }
                catch (ObjectDisposedException)
                {
                    // Ignore shutdown races where dispose happens between action completion and release.
                }
            }
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _gate.Dispose();
    }
}
