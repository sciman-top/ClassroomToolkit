using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;

namespace ClassroomToolkit.Infra.Logging;

public class FileLoggerProvider : ILoggerProvider
{
    private readonly string _logDirectory;
    private readonly Func<DateTime> _nowProvider;
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();
    private readonly BlockingCollection<string> _messageQueue = new();
    private readonly Task _processQueueTask;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private int _disposeState;

    public FileLoggerProvider(string logDirectory, Func<DateTime>? nowProvider = null)
    {
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
        return _loggers.GetOrAdd(categoryName, name => new FileLogger(name, this));
    }

    internal void EnqueueMessage(string message)
    {
        if (Volatile.Read(ref _disposeState) != 0
            || _cancellationTokenSource.IsCancellationRequested
            || _messageQueue.IsAddingCompleted)
        {
            return;
        }

        try
        {
            _messageQueue.Add(message);
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
            foreach (var message in _messageQueue.GetConsumingEnumerable(_cancellationTokenSource.Token))
            {
                try
                {
                    var logFile = Path.Combine(_logDirectory, $"app_{_nowProvider():yyyyMMdd}.log");
                    // Simple append, could be optimized with buffering
                    File.AppendAllText(logFile, message + Environment.NewLine, Encoding.UTF8);
                }
                catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
                {
                    // Fallback or ignore
                }
            }
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
        {
            // Expected during provider disposal.
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
            _cancellationTokenSource.Cancel();
        }
        catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
        {
            // Ignore
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
            _processQueueTask.Wait(1000);
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
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var message = formatter(state, exception);
        var logRecord = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{logLevel}] [{_categoryName}] {message}";
        
        if (exception != null)
        {
             logRecord += Environment.NewLine + exception.ToString();
        }

        _provider.EnqueueMessage(logRecord);
    }
}
