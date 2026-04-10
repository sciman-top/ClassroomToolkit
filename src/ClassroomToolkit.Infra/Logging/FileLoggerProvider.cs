using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;

namespace ClassroomToolkit.Infra.Logging;

public class FileLoggerProvider : ILoggerProvider
{
    private readonly record struct LogQueueItem(DateTime Timestamp, string Message);
    private const int QueueBatchSize = 64;
    private const int QueueDrainTimeoutMs = 3000;
    private const int QueueCancelGraceTimeoutMs = 1000;

    private readonly string _logDirectory;
    private readonly Func<DateTime> _nowProvider;
    private readonly bool _resetExistingLogsOnStartup;
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();
    private readonly BlockingCollection<LogQueueItem> _messageQueue = new();
    private readonly Task _processQueueTask;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private int _disposeState;
    private int _queueResourcesDisposed;

    public FileLoggerProvider(
        string logDirectory,
        Func<DateTime>? nowProvider = null,
        bool resetExistingLogsOnStartup = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logDirectory);
        _logDirectory = logDirectory;
        _nowProvider = nowProvider ?? (() => DateTime.Now);
        _resetExistingLogsOnStartup = resetExistingLogsOnStartup;
        if (!Directory.Exists(_logDirectory))
        {
            Directory.CreateDirectory(_logDirectory);
        }
        TryResetSessionLogs();

        _processQueueTask = Task.Factory.StartNew(
            ProcessQueue,
            _cancellationTokenSource.Token,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }

    public ILogger CreateLogger(string categoryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryName);
        return _loggers.GetOrAdd(categoryName, name => new FileLogger(name, this));
    }

    internal DateTime GetCurrentTime()
    {
        try
        {
            return _nowProvider();
        }
        catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
        {
            return DateTime.Now;
        }
    }

    internal void EnqueueMessage(DateTime timestamp, string message)
    {
        if (Volatile.Read(ref _disposeState) != 0
            || _cancellationTokenSource.IsCancellationRequested
            || _messageQueue.IsAddingCompleted)
        {
            return;
        }

        try
        {
            _messageQueue.Add(new LogQueueItem(timestamp, message));
        }
        catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
        {
            // Ignore queue-close races during shutdown.
        }
    }

    private void ProcessQueue()
    {
        try
        {
            foreach (var item in _messageQueue.GetConsumingEnumerable(_cancellationTokenSource.Token))
            {
                try
                {
                    var batch = BuildQueueBatch(item);
                    FlushBatch(batch);
                }
                catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
                {
                    // Fallback or ignore
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown cancellation.
        }
        catch (ObjectDisposedException)
        {
            // Expected when queue resources are disposed during shutdown races.
        }
    }

    private List<LogQueueItem> BuildQueueBatch(LogQueueItem firstItem)
    {
        var batch = new List<LogQueueItem>(QueueBatchSize)
        {
            firstItem
        };

        while (batch.Count < QueueBatchSize && _messageQueue.TryTake(out var queued))
        {
            batch.Add(queued);
        }

        return batch;
    }

    private void FlushBatch(IReadOnlyList<LogQueueItem> batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (batch.Count == 0)
        {
            return;
        }

        var groupedLines = new Dictionary<string, StringBuilder>(
            capacity: Math.Min(batch.Count, QueueBatchSize),
            comparer: StringComparer.OrdinalIgnoreCase);
        foreach (var item in batch)
        {
            var path = Path.Combine(_logDirectory, $"app_{item.Timestamp:yyyyMMdd}.log");
            if (!groupedLines.TryGetValue(path, out var builder))
            {
                var initialCapacity = Math.Max(256, item.Message.Length + Environment.NewLine.Length);
                builder = new StringBuilder(capacity: initialCapacity);
                groupedLines[path] = builder;
            }

            builder.Append(item.Message);
            builder.Append(Environment.NewLine);
        }

        foreach (var entry in groupedLines)
        {
            try
            {
                File.AppendAllText(entry.Key, entry.Value.ToString(), Encoding.UTF8);
            }
            catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
            {
                // Isolate single-file IO failures so other batched log files still flush.
            }
        }
    }

    private void TryResetSessionLogs()
    {
        if (!_resetExistingLogsOnStartup)
        {
            return;
        }

        try
        {
            foreach (var file in Directory.EnumerateFiles(_logDirectory, "app_*.log", SearchOption.TopDirectoryOnly))
            {
                TryDeleteLogFileBestEffort(file);
            }
        }
        catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
        {
            // Best effort cleanup; continue with current session logging.
        }
    }

    private static void TryDeleteLogFileBestEffort(string filePath)
    {
        try
        {
            File.Delete(filePath);
        }
        catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
        {
            // Best effort cleanup; continue with current session logging.
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
        {
            return;
        }

        try
        {
            _messageQueue.CompleteAdding();
        }
        catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
        {
            // Ignore
        }

        try
        {
            var completed = WaitTaskSafely(_processQueueTask, QueueDrainTimeoutMs);
            if (!completed)
            {
                _cancellationTokenSource.Cancel();
                completed = WaitTaskSafely(_processQueueTask, QueueCancelGraceTimeoutMs);
            }
            if (!completed)
            {
                // Avoid disposing shared queue objects while worker thread is still unwinding.
                _loggers.Clear();
                _ = _processQueueTask.ContinueWith(
                    _ => DisposeQueueResourcesOnce(),
                    CancellationToken.None,
                    TaskContinuationOptions.None,
                    TaskScheduler.Default);
                return;
            }
        }
        catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
        {
            // Ignore
        }

        DisposeQueueResourcesOnce();
        _loggers.Clear();
    }

    private static bool WaitTaskSafely(Task task, int timeoutMs)
    {
        ArgumentNullException.ThrowIfNull(task);
        try
        {
            return task.Wait(timeoutMs);
        }
        catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
        {
            return false;
        }
    }

    private void DisposeQueueResourcesOnce()
    {
        if (Interlocked.Exchange(ref _queueResourcesDisposed, 1) != 0)
        {
            return;
        }

        try
        {
            _cancellationTokenSource.Dispose();
        }
        catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
        {
            // Ignore disposal races during shutdown.
        }

        try
        {
            _messageQueue.Dispose();
        }
        catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
        {
            // Ignore disposal races during shutdown.
        }
    }
}

public class FileLogger : ILogger
{
    private readonly string _categoryName;
    private readonly FileLoggerProvider _provider;

    public FileLogger(string categoryName, FileLoggerProvider provider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryName);
        ArgumentNullException.ThrowIfNull(provider);
        _categoryName = categoryName;
        _provider = provider;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);

        if (!IsEnabled(logLevel))
        {
            return;
        }

        string message;
        try
        {
            message = formatter(state, exception);
        }
        catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
        {
            return;
        }

        var now = _provider.GetCurrentTime();
        var logRecord = $"[{now:yyyy-MM-dd HH:mm:ss.fff}] [{logLevel}] [{_categoryName}] {message}";

        if (exception != null)
        {
            logRecord += Environment.NewLine + exception.ToString();
        }

        _provider.EnqueueMessage(now, logRecord);
    }
}
