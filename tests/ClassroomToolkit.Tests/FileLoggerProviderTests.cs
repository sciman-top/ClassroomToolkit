using System.Collections.Concurrent;
using ClassroomToolkit.Infra.Logging;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace ClassroomToolkit.Tests;

public sealed class FileLoggerProviderTests
{
    [Fact]
    public void Constructor_ShouldThrowArgumentException_WhenLogDirectoryIsBlank()
    {
        Action act = () => _ = new FileLoggerProvider(" ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateLogger_ShouldThrowArgumentException_WhenCategoryNameIsBlank()
    {
        var directory = TestPathHelper.CreateDirectory("ctool_file_logger_category_guard");
        using var provider = new FileLoggerProvider(directory);

        Action act = () => _ = provider.CreateLogger(" ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FileLoggerConstructor_ShouldThrow_WhenProviderIsNull()
    {
        Action act = () => _ = new FileLogger("category", null!);

        act.Should().Throw<ArgumentNullException>();
    }

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

        provider.Dispose();

        var day1Content = File.ReadAllText(day1File);
        var day2Content = File.ReadAllText(day2File);
        day1Content.Should().Contain("message-day-1");
        day2Content.Should().Contain("message-day-2");
    }

    [Fact]
    public void Constructor_ShouldPreserveExistingSessionLog_WhenResetDisabled()
    {
        var directory = TestPathHelper.CreateDirectory("ctool_file_logger_preserve_history");
        var fixedNow = new DateTime(2026, 03, 18, 12, 34, 56);
        var currentFile = Path.Combine(directory, $"app_{fixedNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.log");
        File.WriteAllText(currentFile, "legacy-line" + Environment.NewLine);

        var provider = new FileLoggerProvider(directory, () => fixedNow, resetExistingLogsOnStartup: false, retentionNow: fixedNow);
        var logger = provider.CreateLogger("preserve-history");
        logger.Log(
            LogLevel.Information,
            new EventId(1, "preserve"),
            "fresh-line",
            null,
            static (state, _) => state);

        provider.Dispose();

        var content = File.ReadAllText(currentFile);
        content.Should().Contain("legacy-line");
        content.Should().Contain("fresh-line");
    }

    [Fact]
    public void Constructor_ShouldResetExistingSessionLog_WhenResetEnabled()
    {
        var directory = TestPathHelper.CreateDirectory("ctool_file_logger_reset_history");
        var fixedNow = new DateTime(2026, 03, 19, 08, 30, 00);
        var currentFile = Path.Combine(directory, $"app_{fixedNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.log");
        var previousFile = Path.Combine(directory, "app_20260318.log");
        File.WriteAllText(currentFile, "legacy-current-line" + Environment.NewLine);
        File.WriteAllText(previousFile, "legacy-previous-line" + Environment.NewLine);

        var provider = new FileLoggerProvider(directory, () => fixedNow, resetExistingLogsOnStartup: true);
        var logger = provider.CreateLogger("reset-history");
        logger.Log(
            LogLevel.Information,
            new EventId(1, "reset"),
            "fresh-line",
            null,
            static (state, _) => state);

        provider.Dispose();

        File.Exists(previousFile).Should().BeFalse();
        var content = File.ReadAllText(currentFile);
        content.Should().NotContain("legacy-current-line");
        content.Should().Contain("fresh-line");
    }

    [Fact]
    public void Dispose_ShouldFlushQueuedMessages_BeforeShutdown()
    {
        var directory = TestPathHelper.CreateDirectory("ctool_file_logger_flush");
        var now = new DateTime(2026, 03, 16, 12, 0, 0);
        var file = Path.Combine(directory, $"app_{now.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.log");

        var provider = new FileLoggerProvider(directory, () => now);
        var logger = provider.CreateLogger("flush-category");
        const int total = 2500;
        for (var index = 0; index < total; index++)
        {
            logger.Log(
                LogLevel.Information,
                new EventId(index, "flush"),
                $"flush-message-{index}",
                null,
                static (state, _) => state);
        }

        provider.Dispose();

        File.Exists(file).Should().BeTrue();
        var content = File.ReadAllText(file);
        var lines = content.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines.Length.Should().Be(total);
        content.Should().Contain("flush-message-0");
        content.Should().Contain($"flush-message-{total - 1}");
    }

    [Fact]
    public void Log_ShouldUseProviderTime_ForTimestampAndFileBucket()
    {
        var directory = TestPathHelper.CreateDirectory("ctool_file_logger_time_source");
        var fixedNow = new DateTime(2026, 03, 17, 01, 02, 03, 456);
        var expectedDateToken = fixedNow.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
        var expectedFile = Path.Combine(directory, $"app_{fixedNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.log");

        var provider = new FileLoggerProvider(directory, () => fixedNow);
        var logger = provider.CreateLogger("time-source");
        logger.Log(
            LogLevel.Information,
            new EventId(1, "time-source"),
            "time-source-message",
            null,
            static (state, _) => state);

        SpinWait.SpinUntil(() => File.Exists(expectedFile), TimeSpan.FromSeconds(2)).Should().BeTrue();
        provider.Dispose();
        var content = File.ReadAllText(expectedFile);
        content.Should().Contain(expectedDateToken);
        content.Should().Contain("time-source-message");
    }

    [Fact]
    public void Constructor_ShouldPruneExpiredAppLogs_WhenRetentionApplies()
    {
        var directory = TestPathHelper.CreateDirectory("ctool_file_logger_retention_expired");
        var now = new DateTime(2026, 04, 22, 10, 00, 00);
        var expiredFile = Path.Combine(directory, "app_20260407.log");
        var retainedFile = Path.Combine(directory, "app_20260409.log");
        var todayFile = Path.Combine(directory, "app_20260422.log");
        File.WriteAllText(expiredFile, "expired");
        File.WriteAllText(retainedFile, "retained");
        File.WriteAllText(todayFile, "today");

        using var provider = new FileLoggerProvider(directory, () => now, retentionNow: now);

        File.Exists(expiredFile).Should().BeFalse();
        File.Exists(retainedFile).Should().BeTrue();
        File.Exists(todayFile).Should().BeTrue();
    }

    [Fact]
    public void Constructor_ShouldPruneOversizedHistoricalAppLog_ButPreserveToday()
    {
        var directory = TestPathHelper.CreateDirectory("ctool_file_logger_retention_oversized");
        var now = new DateTime(2026, 04, 22, 10, 00, 00);
        var options = new LogRetentionOptions(RetentionDays: 14, MaxHistoricalFileBytes: 8);
        var oversizedHistoricalFile = Path.Combine(directory, "app_20260421.log");
        var oversizedTodayFile = Path.Combine(directory, "app_20260422.log");
        File.WriteAllText(oversizedHistoricalFile, "0123456789");
        File.WriteAllText(oversizedTodayFile, "0123456789");

        using var provider = new FileLoggerProvider(directory, () => now, retentionOptions: options, retentionNow: now);

        File.Exists(oversizedHistoricalFile).Should().BeFalse();
        File.Exists(oversizedTodayFile).Should().BeTrue();
    }

    [Fact]
    public void LogRetentionPolicy_ShouldApplySameRetentionToErrorLogs()
    {
        var directory = TestPathHelper.CreateDirectory("ctool_error_logger_retention");
        var now = new DateTime(2026, 04, 22, 10, 00, 00);
        var expiredErrorFile = Path.Combine(directory, "error_20260401.log");
        var retainedErrorFile = Path.Combine(directory, "error_20260422.log");
        var unrelatedFile = Path.Combine(directory, "notes_20260401.log");
        File.WriteAllText(expiredErrorFile, "expired");
        File.WriteAllText(retainedErrorFile, "retained");
        File.WriteAllText(unrelatedFile, "unrelated");

        LogRetentionPolicy.TryApply(directory, "error_", now);

        File.Exists(expiredErrorFile).Should().BeFalse();
        File.Exists(retainedErrorFile).Should().BeTrue();
        File.Exists(unrelatedFile).Should().BeTrue();
    }

    [Fact]
    public void Log_ShouldNotThrow_WhenNowProviderThrowsNonFatal()
    {
        var directory = TestPathHelper.CreateDirectory("ctool_file_logger_now_provider_nonfatal");
        using var provider = new FileLoggerProvider(directory, () => throw new InvalidOperationException("now-failed"));
        var logger = provider.CreateLogger("now-provider-nonfatal");

        Action act = () => logger.Log(
            LogLevel.Information,
            new EventId(1, "now-provider"),
            "message",
            null,
            static (state, _) => state);

        act.Should().NotThrow();
    }

    [Fact]
    public void Log_ShouldNotThrow_WhenFormatterThrowsNonFatal()
    {
        var directory = TestPathHelper.CreateDirectory("ctool_file_logger_formatter_nonfatal");
        using var provider = new FileLoggerProvider(directory);
        var logger = provider.CreateLogger("formatter-nonfatal");

        Action act = () => logger.Log(
            LogLevel.Information,
            new EventId(1, "formatter"),
            "message",
            null,
            static (_, _) => throw new InvalidOperationException("formatter-failed"));

        act.Should().NotThrow();
    }
}
