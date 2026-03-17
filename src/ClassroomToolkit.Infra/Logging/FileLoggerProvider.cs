using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;

namespace ClassroomToolkit.Infra.Logging;

public class FileLoggerProvider : ILoggerProvider
{
    private readonly record struct LogQueueItem(DateTime Timestamp, string Message);

    private readonly string _logDirectory;
    private readonly Func<DateTime> _nowProvider;
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();
    private readonly BlockingCollection<LogQueueItem> _messageQueue = new();
    private readonly Task _processQueueTask;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private int _disposeState;

    public FileLoggerProvider(string logDirectory, Func<DateTime>? nowProvider = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logDirectory);
        _logDirectory = logDirectory;
        _nowProvider = nowProvider ?? (() => DateTime.Now);
        if (!Directory.Exists(_logDirectory))
        {
            Directory.CreateDirectory(_logDirectory);
        }

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
        catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
        {
            // Expected when the provider forces cancellation after a shutdown timeout.
        }
        catch (ObjectDisposedException)
        {
            // Expected when queue resources are disposed during shutdown races.
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
            if (!_processQueueTask.Wait(3000))
            {
                _cancellationTokenSource.Cancel();
                _processQueueTask.Wait(1000);
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
