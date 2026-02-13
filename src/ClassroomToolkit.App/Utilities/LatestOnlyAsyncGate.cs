using System.Threading;

namespace ClassroomToolkit.App.Utilities;

internal sealed class LatestOnlyAsyncGate
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private int _generation;

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
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!IsCurrent(generation))
            {
                return;
            }
            await action(() => IsCurrent(generation)).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }
}
