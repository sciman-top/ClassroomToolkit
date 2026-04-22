using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace ClassroomToolkit.Infra.Logging;

public class FileLoggerProvider : ILoggerProvider
{
    private readonly record struct LogQueueItem(DateTime Timestamp, string Message);
    private const int LogQueueCapacity = 8192;
    private const int QueueBatchSize = 64;
    private const int QueueDrainTimeoutMs = 3000;
    private const int QueueCancelGraceTimeoutMs = 1000;

    private readonly string _logDirectory;
    private readonly Func<DateTime> _nowProvider;
    private readonly bool _resetExistingLogsOnStartup;
    private readonly LogRetentionOptions _retentionOptions;
    private readonly DateTime? _retentionNow;
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();
    private readonly BlockingCollection<LogQueueItem> _messageQueue = new(LogQueueCapacity);
    private readonly Task _processQueueTask;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private int _disposeState;
    private int _queueResourcesDisposed;
    private long _droppedMessageCount;

    public FileLoggerProvider(
        string logDirectory,
        Func<DateTime>? nowProvider = null,
        bool resetExistingLogsOnStartup = false,
        LogRetentionOptions? retentionOptions = null,
        DateTime? retentionNow = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logDirectory);
        _logDirectory = logDirectory;
        _nowProvider = nowProvider ?? (() => DateTime.Now);
        _resetExistingLogsOnStartup = resetExistingLogsOnStartup;
        _retentionOptions = retentionOptions ?? new LogRetentionOptions();
        _retentionNow = retentionNow;
        if (!Directory.Exists(_logDirectory))
        {
            Directory.CreateDirectory(_logDirectory);
        }
        TryResetSessionLogs();
        TryApplyRetention();

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
            if (!_messageQueue.TryAdd(new LogQueueItem(timestamp, message)))
            {
                var dropped = Interlocked.Increment(ref _droppedMessageCount);
                if ((dropped & 0x3F) == 1)
                {
                    Debug.WriteLine($"[FileLoggerProvider] Queue full. Dropped log messages={dropped}.");
                }
            }
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
                    Debug.WriteLine($"[FileLoggerProvider] Flush batch failed: {ex.GetType().Name} - {ex.Message}");
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
        catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine($"[FileLoggerProvider] Queue worker stopped unexpectedly: {ex.GetType().Name} - {ex.Message}");
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
                Debug.WriteLine($"[FileLoggerProvider] Append failed: {ex.GetType().Name} - {ex.Message}");
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

    private void TryApplyRetention()
    {
        LogRetentionPolicy.TryApply(
            _logDirectory,
            "app_",
            ResolveRetentionReferenceTime(),
            _retentionOptions);
    }

    private DateTime ResolveRetentionReferenceTime()
    {
        if (_retentionNow.HasValue)
        {
            return _retentionNow.Value;
        }

        return GetCurrentTime();
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
        GC.SuppressFinalize(this);

        try
        {
            _messageQueue.CompleteAdding();
        }
        catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine($"[FileLoggerProvider] CompleteAdding failed during dispose: {ex.GetType().Name} - {ex.Message}");
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
                    _ =>
                    {
                        TryWriteDroppedMessageSummary();
                        DisposeQueueResourcesOnce();
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.None,
                    TaskScheduler.Default);
                return;
            }
        }
        catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine($"[FileLoggerProvider] Dispose shutdown wait failed: {ex.GetType().Name} - {ex.Message}");
        }

        TryWriteDroppedMessageSummary();
        DisposeQueueResourcesOnce();
        _loggers.Clear();
    }

    private static bool WaitTaskSafely(Task task, int timeoutMs)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentOutOfRangeException.ThrowIfLessThan(timeoutMs, Timeout.Infinite);

        if (timeoutMs == 0)
        {
            return EvaluateTaskCompletion(task);
        }

        try
        {
            var asyncResult = (IAsyncResult)task;
            var waitHandle = asyncResult.AsyncWaitHandle;
            if (timeoutMs == Timeout.Infinite)
            {
                waitHandle.WaitOne();
                return EvaluateTaskCompletion(task);
            }

            if (!waitHandle.WaitOne(timeoutMs))
            {
                return false;
            }
        }
        catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine($"[FileLoggerProvider] Queue wait error: {ex.GetType().Name} - {ex.Message}");
            return false;
        }

        return EvaluateTaskCompletion(task);
    }

    private static bool EvaluateTaskCompletion(Task task)
    {
        if (task.IsCompletedSuccessfully)
        {
            return true;
        }

        if (task.IsCanceled)
        {
            Debug.WriteLine("[FileLoggerProvider] Queue wait canceled.");
            return false;
        }

        var failure = task.Exception?.GetBaseException();
        if (failure != null && InfraExceptionFilterPolicy.IsNonFatal(failure))
        {
            Debug.WriteLine($"[FileLoggerProvider] Queue wait failed: {failure.GetType().Name} - {failure.Message}");
        }

        return false;
    }

    private void TryWriteDroppedMessageSummary()
    {
        var dropped = Interlocked.Read(ref _droppedMessageCount);
        if (dropped <= 0)
        {
            return;
        }

        var now = GetCurrentTime();
        var logPath = Path.Combine(_logDirectory, $"app_{now:yyyyMMdd}.log");
        var summaryLine = $"[{now:yyyy-MM-dd HH:mm:ss.fff}] [Warning] [FileLoggerProvider] dropped-log-messages={dropped}";
        try
        {
            File.AppendAllText(logPath, summaryLine + Environment.NewLine, Encoding.UTF8);
        }
        catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine($"[FileLoggerProvider] Drop summary append failed: {ex.GetType().Name} - {ex.Message}");
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
            Debug.WriteLine($"[FileLoggerProvider] CancellationTokenSource dispose failed: {ex.GetType().Name} - {ex.Message}");
        }

        try
        {
            _messageQueue.Dispose();
        }
        catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine($"[FileLoggerProvider] Message queue dispose failed: {ex.GetType().Name} - {ex.Message}");
        }
    }
}

public class FileLogger : ILogger
{
    private static readonly char[] UnsafeLogCharacters = ['\r', '\n', '\0'];
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
        var safeCategory = SanitizeSingleLine(_categoryName);
        var safeMessage = SanitizeSingleLine(message);
        var logRecord = $"[{now:yyyy-MM-dd HH:mm:ss.fff}] [{logLevel}] [{safeCategory}] {safeMessage}";

        if (exception != null)
        {
            logRecord += Environment.NewLine + exception.ToString();
        }

        _provider.EnqueueMessage(now, logRecord);
    }

    private static string SanitizeSingleLine(string value)
    {
        if (string.IsNullOrEmpty(value) || value.IndexOfAny(UnsafeLogCharacters) < 0)
        {
            return value;
        }

        var builder = new StringBuilder(value.Length + 8);
        foreach (var ch in value)
        {
            _ = ch switch
            {
                '\r' => builder.Append("\\r"),
                '\n' => builder.Append("\\n"),
                '\0' => builder.Append("\\0"),
                _ => builder.Append(ch)
            };
        }

        return builder.ToString();
    }
}
