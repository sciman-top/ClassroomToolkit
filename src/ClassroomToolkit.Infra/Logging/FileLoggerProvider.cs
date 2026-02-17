using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.IO;
using System.Text;

namespace ClassroomToolkit.Infra.Logging;

public class FileLoggerProvider : ILoggerProvider
{
    private readonly string _logDirectory;
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();
    private readonly BlockingCollection<string> _messageQueue = new();
    private readonly Task _processQueueTask;
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    public FileLoggerProvider(string logDirectory)
    {
        _logDirectory = logDirectory;
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
        if (!_cancellationTokenSource.IsCancellationRequested)
        {
            _messageQueue.Add(message);
        }
    }

    private void ProcessQueue()
    {
        var logFile = Path.Combine(_logDirectory, $"app_{DateTime.Now:yyyyMMdd}.log");

        foreach (var message in _messageQueue.GetConsumingEnumerable(_cancellationTokenSource.Token))
        {
            try
            {
                // Simple append, could be optimized with buffering
                File.AppendAllText(logFile, message + Environment.NewLine, Encoding.UTF8);
            }
            catch
            {
                // Fallback or ignore
            }
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _messageQueue.CompleteAdding();
        try
        {
            _processQueueTask.Wait(1000);
        }
        catch
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
