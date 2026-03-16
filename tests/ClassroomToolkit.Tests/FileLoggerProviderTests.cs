using System.Collections.Concurrent;
using ClassroomToolkit.Infra.Logging;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace ClassroomToolkit.Tests;

public sealed class FileLoggerProviderTests
{
    [Fact]
    public void Dispose_ShouldBeIdempotent()
    {
        var directory = TestPathHelper.CreateDirectory("ctool_file_logger_dispose");
        using var provider = new FileLoggerProvider(directory);

        Action act = () =>
        {
            provider.Dispose();
            provider.Dispose();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void Log_ShouldNotThrow_AfterProviderDisposed()
    {
        var directory = TestPathHelper.CreateDirectory("ctool_file_logger_after_dispose");
        var provider = new FileLoggerProvider(directory);
        var logger = provider.CreateLogger("test-category");
        provider.Dispose();

        Action act = () => logger.Log(
            LogLevel.Information,
            new EventId(1, "test"),
            "message-after-dispose",
            null,
            static (state, _) => state);

        act.Should().NotThrow();
    }

    [Fact]
    public async Task Log_ShouldNotThrow_WhenDisposeRacesWithConcurrentWrites()
    {
        var directory = TestPathHelper.CreateDirectory("ctool_file_logger_concurrent_dispose");
        var provider = new FileLoggerProvider(directory);
        var logger = provider.CreateLogger("test-category");
        var failures = new ConcurrentQueue<Exception>();

        var writers = Enumerable.Range(0, 4)
            .Select(_ => Task.Run(() =>
            {
                for (var index = 0; index < 200; index++)
                {
                    try
                    {
                        logger.Log(
                            LogLevel.Information,
                            new EventId(index, "race"),
                            $"message-{index}",
                            null,
                            static (state, _) => state);
                    }
                    catch (Exception ex)
                    {
                        failures.Enqueue(ex);
                    }
                }
            }))
            .ToArray();

        // Force a concurrent shutdown while writers are still active.
        provider.Dispose();

        await Task.WhenAll(writers);

        failures.Should().BeEmpty();
    }

    [Fact]
    public void Log_ShouldRotateFile_WhenDateChanges()
    {
        var directory = TestPathHelper.CreateDirectory("ctool_file_logger_rotate");
        var day1 = new DateTime(2026, 03, 15, 23, 59, 59);
        var day2 = day1.AddSeconds(2);
        var nowQueue = new Queue<DateTime>([day1, day2, day2]);
        var nowSync = new object();

        DateTime NextNow()
        {
            lock (nowSync)
            {
                return nowQueue.Count > 0 ? nowQueue.Dequeue() : day2;
            }
        }

        using var provider = new FileLoggerProvider(directory, NextNow);
        var logger = provider.CreateLogger("rotate-category");

        logger.Log(
            LogLevel.Information,
            new EventId(1, "day1"),
            "message-day-1",
            null,
            static (state, _) => state);
        logger.Log(
            LogLevel.Information,
            new EventId(2, "day2"),
            "message-day-2",
            null,
            static (state, _) => state);

        var day1File = Path.Combine(directory, $"app_{day1.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.log");
        var day2File = Path.Combine(directory, $"app_{day2.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.log");

        SpinWait.SpinUntil(
            () => File.Exists(day1File) && File.Exists(day2File),
            TimeSpan.FromSeconds(2)).Should().BeTrue();

        var day1Content = File.ReadAllText(day1File);
        var day2Content = File.ReadAllText(day2File);
        day1Content.Should().Contain("message-day-1");
        day2Content.Should().Contain("message-day-2");
    }
}
