using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ClassroomToolkit.Application.UseCases.RollCall;

public sealed class RollCallWorkbookLoadOrchestrator
{
    private readonly RollCallWorkbookUseCase _workbookUseCase;
    private readonly object _preloadLock = new();
    private Task<RollCallWorkbookLoadResult>? _preloadTask;
    private RollCallWorkbookLoadResult? _preloadedResult;
    private string? _preloadedPath;
    private DateTime _preloadedWriteTimeUtc;

    public RollCallWorkbookLoadOrchestrator(RollCallWorkbookUseCase workbookUseCase)
    {
        _workbookUseCase = workbookUseCase ?? throw new ArgumentNullException(nameof(workbookUseCase));
    }

    public void Warmup(string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        if (!File.Exists(path)) return;

        if (!TryGetFileWriteTimeUtc(path, out var writeTimeUtc))
        {
            return;
        }

        lock (_preloadLock)
        {
            if (_preloadedResult != null && (!string.Equals(_preloadedPath, path, StringComparison.OrdinalIgnoreCase) || _preloadedWriteTimeUtc != writeTimeUtc))
            {
                _preloadedResult = null;
            }
            if (_preloadedResult != null && string.Equals(_preloadedPath, path, StringComparison.OrdinalIgnoreCase) && _preloadedWriteTimeUtc == writeTimeUtc)
            {
                return;
            }
            if (_preloadTask != null && string.Equals(_preloadedPath, path, StringComparison.OrdinalIgnoreCase) && _preloadedWriteTimeUtc == writeTimeUtc)
            {
                return;
            }

            _preloadedPath = path;
            _preloadedWriteTimeUtc = writeTimeUtc;
            var expectedPath = path;
            var expectedWriteTimeUtc = writeTimeUtc;
            var preloadTask = Task.Run(() => _workbookUseCase.Load(expectedPath), cancellationToken);
            _preloadTask = preloadTask;
            preloadTask.ContinueWith(task =>
            {
                lock (_preloadLock)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        if (ReferenceEquals(_preloadTask, preloadTask))
                        {
                            _preloadTask = null;
                        }
                        return;
                    }
                    if (task.Status == TaskStatus.RanToCompletion)
                    {
                        var completedResult = task.GetAwaiter().GetResult();
                        if (string.Equals(_preloadedPath, expectedPath, StringComparison.OrdinalIgnoreCase) && _preloadedWriteTimeUtc == expectedWriteTimeUtc)
                        {
                            if (string.IsNullOrWhiteSpace(completedResult.ErrorMessage))
                            {
                                _preloadedResult = completedResult;
                            }
                        }
                    }
                    if (ReferenceEquals(_preloadTask, preloadTask))
                    {
                        _preloadTask = null;
                    }
                }
            }, TaskScheduler.Default);
        }
    }

    public RollCallWorkbookLoadResult Load(string path)
    {
        return TryConsumePreloaded(path) ?? _workbookUseCase.Load(path);
    }

    public RollCallWorkbookLoadResult? TryConsumePreloaded(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;

        if (!TryGetFileWriteTimeUtc(path, out var writeTimeUtc))
        {
            return null;
        }

        Task<RollCallWorkbookLoadResult>? preloadTask = null;
        bool consumeTask = false;

        lock (_preloadLock)
        {
            if (!string.Equals(_preloadedPath, path, StringComparison.OrdinalIgnoreCase) || _preloadedWriteTimeUtc != writeTimeUtc)
            {
                _preloadedResult = null;
                return null;
            }
            if (_preloadedResult != null)
            {
                var result = _preloadedResult;
                _preloadedResult = null;
                return result;
            }
            if (_preloadTask != null && _preloadTask.IsCompleted)
            {
                preloadTask = _preloadTask;
                _preloadTask = null;
                consumeTask = true;
            }
        }

        if (preloadTask != null && consumeTask)
        {
            try
            {
                if (!preloadTask.IsCompletedSuccessfully) return null;
                var result = preloadTask.GetAwaiter().GetResult();
                if (!string.IsNullOrWhiteSpace(result.ErrorMessage)) return null;
                return result;
            }
            catch (Exception ex) when (ApplicationExceptionFilterPolicy.IsNonFatal(ex))
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[RollCallDataLoad] preload-consume-failed path={path} ex={ex.GetType().Name} msg={ex.Message}");
                return null;
            }
        }

        return null;
    }

    public void Reset()
    {
        lock (_preloadLock)
        {
            _preloadTask = null;
            _preloadedResult = null;
            _preloadedPath = null;
            _preloadedWriteTimeUtc = default;
        }
    }

    private static bool TryGetFileWriteTimeUtc(string path, out DateTime writeTimeUtc)
    {
        writeTimeUtc = default;
        try
        {
            writeTimeUtc = File.GetLastWriteTimeUtc(path);
            return true;
        }
        catch (Exception ex) when (ApplicationExceptionFilterPolicy.IsNonFatal(ex))
        {
            System.Diagnostics.Debug.WriteLine(
                $"[RollCallDataLoad] file-write-time-read-failed path={path} ex={ex.GetType().Name} msg={ex.Message}");
            return false;
        }
    }
}
