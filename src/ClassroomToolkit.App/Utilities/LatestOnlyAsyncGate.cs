using System.Threading;

namespace ClassroomToolkit.App.Utilities;

internal sealed class LatestOnlyAsyncGate : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _disposeCancellation = new();
    private readonly object _lifetimeLock = new();
    private int _generation;
    private int _disposed;
    private int _activeOperations;
    private bool _resourcesDisposed;

    public int NextGeneration()
    {
        return Interlocked.Increment(ref _generation);
    }

    public bool IsCurrent(int generation)
    {
        return Volatile.Read(ref _disposed) == 0
            && Volatile.Read(ref _generation) == generation;
    }

    /// <param name="continueOnCapturedContext">
    /// true 时 await 续接回调用方上下文（UI 线程）。钩子启动协程必须传 true：
    /// WH_*_LL 钩子要求安装线程持续泵消息，落到线程池线程会静默失效。
    /// 纯后台工作（如墨迹边车写盘）保持 false，避免文件 IO 回到 UI 线程。
    /// </param>
    public async Task RunAsync(
        int generation,
        Func<Func<bool>, Task> action,
        bool continueOnCapturedContext = false)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        CancellationToken disposalToken;
        lock (_lifetimeLock)
        {
            if (_disposed != 0)
            {
                return;
            }

            _activeOperations++;
            disposalToken = _disposeCancellation.Token;
        }

        var entered = false;
        try
        {
            try
            {
                await _gate.WaitAsync(disposalToken).ConfigureAwait(continueOnCapturedContext);
                entered = true;
            }
            catch (OperationCanceledException) when (disposalToken.IsCancellationRequested)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            if (Volatile.Read(ref _disposed) != 0)
            {
                return;
            }
            if (!IsCurrent(generation))
            {
                return;
            }
            await action(() => IsCurrent(generation)).ConfigureAwait(continueOnCapturedContext);
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

            CompleteOperation();
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        var disposeResourcesNow = false;
        lock (_lifetimeLock)
        {
            if (_activeOperations == 0 && !_resourcesDisposed)
            {
                _resourcesDisposed = true;
                disposeResourcesNow = true;
            }
        }

        try
        {
            _disposeCancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The final in-flight operation may have completed and released resources first.
        }

        if (disposeResourcesNow)
        {
            DisposeResources();
        }
    }

    private void CompleteOperation()
    {
        var disposeResourcesNow = false;
        lock (_lifetimeLock)
        {
            _activeOperations--;
            if (_disposed != 0 && _activeOperations == 0 && !_resourcesDisposed)
            {
                _resourcesDisposed = true;
                disposeResourcesNow = true;
            }
        }

        if (disposeResourcesNow)
        {
            DisposeResources();
        }
    }

    private void DisposeResources()
    {
        _disposeCancellation.Dispose();
        _gate.Dispose();
    }
}
