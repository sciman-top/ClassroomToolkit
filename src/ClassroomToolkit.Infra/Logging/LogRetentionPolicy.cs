using System.Globalization;
using System.IO;

namespace ClassroomToolkit.Infra.Logging;

public static class LogRetentionPolicy
{
    public static void TryApply(
        string logDirectory,
        string filePrefix,
        DateTime now,
        LogRetentionOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(logDirectory)
            || string.IsNullOrWhiteSpace(filePrefix)
            || !Directory.Exists(logDirectory))
        {
            return;
        }

        var effectiveOptions = options ?? new LogRetentionOptions();
        var today = now.Date;
        var oldestRetainedDate = today.AddDays(-(effectiveOptions.EffectiveRetentionDays - 1));

        try
        {
            foreach (var file in Directory.EnumerateFiles(logDirectory, $"{filePrefix}*.log", SearchOption.TopDirectoryOnly))
            {
                TryDeleteIfExpiredOrOversized(file, filePrefix, today, oldestRetainedDate, effectiveOptions);
            }
        }
        catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
        {
            // Best effort cleanup only; logging must never fail because retention failed.
        }
    }

    private static void TryDeleteIfExpiredOrOversized(
        string filePath,
        string filePrefix,
        DateTime today,
        DateTime oldestRetainedDate,
        LogRetentionOptions options)
    {
        try
        {
            var logDate = TryParseLogDate(filePath, filePrefix);
            if (logDate is null)
            {
                return;
            }

            if (logDate.Value.Date == today)
            {
                return;
            }

            var file = new FileInfo(filePath);
            if (logDate.Value.Date < oldestRetainedDate
                || file.Length > options.EffectiveMaxHistoricalFileBytes)
            {
                File.Delete(filePath);
            }
        }
        catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
        {
            // Ignore individual file failures so one locked file does not block the app.
        }
    }

    private static DateTime? TryParseLogDate(string filePath, string filePrefix)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        if (!fileName.StartsWith(filePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var dateToken = fileName[filePrefix.Length..];
        return DateTime.TryParseExact(
            dateToken,
            "yyyyMMdd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsed)
            ? parsed.Date
            : null;
    }
}
