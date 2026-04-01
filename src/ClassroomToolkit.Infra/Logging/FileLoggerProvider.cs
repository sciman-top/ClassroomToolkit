using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;

namespace ClassroomToolkit.Infra.Logging;

public class FileLoggerProvider : ILoggerProvider
{
    private readonly record struct LogQueueItem(DateTime Timestamp, string Message);
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
            foreach (var item in _messageQueue.GetConsumingEnumerable())
            {
                try
                {
                    var logFile = Path.Combine(_logDirectory, $"app_{item.Timestamp:yyyyMMdd}.log");
                    // Simple append, could be optimized with buffering
                    File.AppendAllText(logFile, item.Message + Environment.NewLine, Encoding.UTF8);
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
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
                {
                    // Best effort cleanup; continue with current session logging.
                }
            }
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
                return;
            }
        }
        catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
        {
            // Ignore
        }

        _cancellationTokenSource.Dispose();
        _messageQueue.Dispose();
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
