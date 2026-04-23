using System.Text.Json;
using System.Threading;
using System.Diagnostics;
using System.Security.Cryptography;
using ClassroomToolkit.Application.Abstractions;
using ClassroomToolkit.Domain.Utilities;

namespace ClassroomToolkit.Infra.Settings;

public sealed class JsonSettingsDocumentStoreAdapter : ISettingsDocumentStore
{
    private const long MaxSettingsFileBytes = 4L * 1024 * 1024;
    private static long _oversizedSettingsRejectCount;
    private readonly string _path;
    private int _hasValidatedExistingFileState;
    private int _overwriteBlockedAfterCorruptLoad;
    private long _lastValidatedWriteTimeUtcTicks = DateTime.MinValue.Ticks;
    private string? _lastValidatedContentHash;

    public JsonSettingsDocumentStoreAdapter(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
    }

    public Dictionary<string, Dictionary<string, string>> Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                Interlocked.Exchange(ref _hasValidatedExistingFileState, 0);
                Interlocked.Exchange(ref _overwriteBlockedAfterCorruptLoad, 0);
                Interlocked.Exchange(ref _lastValidatedWriteTimeUtcTicks, DateTime.MinValue.Ticks);
                _lastValidatedContentHash = null;
                return new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            }

            EnsureInputSizeWithinLimit(operation: "load");

            using var stream = File.OpenRead(_path);
            using var document = JsonDocument.Parse(stream);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                Interlocked.Exchange(ref _hasValidatedExistingFileState, 1);
                Interlocked.Exchange(ref _overwriteBlockedAfterCorruptLoad, 0);
                Interlocked.Exchange(ref _lastValidatedWriteTimeUtcTicks, GetCurrentWriteTimeUtcTicks());
                _lastValidatedContentHash = GetCurrentContentHash();
                return new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            }

            var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var sectionNode in document.RootElement.EnumerateObject())
            {
                if (sectionNode.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var section = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in sectionNode.Value.EnumerateObject())
                {
                    section[item.Name] = item.Value.ValueKind switch
                    {
                        JsonValueKind.String => item.Value.GetString() ?? string.Empty,
                        JsonValueKind.True => "True",
                        JsonValueKind.False => "False",
                        JsonValueKind.Number => item.Value.GetRawText(),
                        JsonValueKind.Null => string.Empty,
                        _ => item.Value.GetRawText()
                    };
                }

                result[sectionNode.Name] = section;
            }

            Interlocked.Exchange(ref _hasValidatedExistingFileState, 1);
            Interlocked.Exchange(ref _overwriteBlockedAfterCorruptLoad, 0);
            Interlocked.Exchange(ref _lastValidatedWriteTimeUtcTicks, GetCurrentWriteTimeUtcTicks());
            _lastValidatedContentHash = GetCurrentContentHash();
            return result;
        }
        catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
        {
            RecordLoadFailure(ex, "load");
            return new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public void Save(Dictionary<string, Dictionary<string, string>> data)
    {
        ArgumentNullException.ThrowIfNull(data);
        EnsureExistingFileStateValidated();
        if (File.Exists(_path) && Volatile.Read(ref _overwriteBlockedAfterCorruptLoad) == 1)
        {
            throw new InvalidOperationException(
                "Settings load detected JSON corruption; refusing to overwrite existing JSON settings file.");
        }

        var parent = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(parent))
        {
            Directory.CreateDirectory(parent);
        }

        var options = new JsonWriterOptions
        {
            Indented = true
        };

        AtomicFileReplaceUtility.WriteAtomically(
            _path,
            tempPath =>
            {
                using var stream = File.Open(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);
                using var writer = new Utf8JsonWriter(stream, options);
                writer.WriteStartObject();
                foreach (var section in data.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
                {
                    writer.WritePropertyName(section.Key);
                    writer.WriteStartObject();
                    var sectionData = section.Value ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var item in sectionData.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
                    {
                        writer.WriteString(item.Key, item.Value);
                    }
                    writer.WriteEndObject();
                }
                writer.WriteEndObject();
                writer.Flush();
            },
            onTempCleanupFailure: static (tempPath, ex) =>
            {
                Debug.WriteLine(
                    $"[JsonSettingsDocumentStoreAdapter] temp cleanup failed path={tempPath} ex={ex.GetType().Name} msg={ex.Message}");
            });

        Interlocked.Exchange(ref _hasValidatedExistingFileState, 1);
        Interlocked.Exchange(ref _overwriteBlockedAfterCorruptLoad, 0);
        Interlocked.Exchange(ref _lastValidatedWriteTimeUtcTicks, GetCurrentWriteTimeUtcTicks());
        _lastValidatedContentHash = GetCurrentContentHash();
    }

    public static long OversizedSettingsRejectCount => Interlocked.Read(ref _oversizedSettingsRejectCount);

    private static bool ShouldBlockOverwriteAfterLoadFailure(Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);
        return ex is JsonException or InvalidDataException;
    }

    private void EnsureExistingFileStateValidated()
    {
        if (!File.Exists(_path))
        {
            Interlocked.Exchange(ref _hasValidatedExistingFileState, 0);
            Interlocked.Exchange(ref _lastValidatedWriteTimeUtcTicks, DateTime.MinValue.Ticks);
            _lastValidatedContentHash = null;
            return;
        }

        var currentWriteTimeUtcTicks = GetCurrentWriteTimeUtcTicks();
        var currentContentHash = TryGetCurrentContentHash();
        if (Interlocked.CompareExchange(ref _hasValidatedExistingFileState, 1, 1) == 1
            && Interlocked.Read(ref _lastValidatedWriteTimeUtcTicks) == currentWriteTimeUtcTicks
            && string.Equals(_lastValidatedContentHash, currentContentHash, StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            EnsureInputSizeWithinLimit(operation: "save-preflight");

            using var stream = File.OpenRead(_path);
            using var document = JsonDocument.Parse(stream);
            Interlocked.Exchange(ref _overwriteBlockedAfterCorruptLoad, 0);
            Interlocked.Exchange(ref _lastValidatedWriteTimeUtcTicks, currentWriteTimeUtcTicks);
            _lastValidatedContentHash = currentContentHash ?? GetCurrentContentHash();
        }
        catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
        {
            RecordLoadFailure(ex, "save-preflight");
        }
        finally
        {
            Interlocked.Exchange(ref _hasValidatedExistingFileState, 1);
        }
    }

    private void RecordLoadFailure(Exception ex, string operation)
    {
        Interlocked.Exchange(ref _hasValidatedExistingFileState, 1);
        Interlocked.Exchange(
            ref _overwriteBlockedAfterCorruptLoad,
            ShouldBlockOverwriteAfterLoadFailure(ex) ? 1 : 0);
        Debug.WriteLine(
            $"[JsonSettingsDocumentStoreAdapter] {operation} failed path={_path} ex={ex.GetType().Name} msg={ex.Message}");
    }

    private long GetCurrentWriteTimeUtcTicks()
    {
        return File.Exists(_path)
            ? File.GetLastWriteTimeUtc(_path).Ticks
            : DateTime.MinValue.Ticks;
    }

    private string? TryGetCurrentContentHash()
    {
        try
        {
            return GetCurrentContentHash();
        }
        catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
        {
            return null;
        }
    }

    private string? GetCurrentContentHash()
    {
        if (!File.Exists(_path))
        {
            return null;
        }

        EnsureInputSizeWithinLimit(operation: "hash");

        using var stream = File.OpenRead(_path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static bool TryValidateInputSize(string path, out long fileLength, out bool exceedsLimit)
    {
        fileLength = 0;
        exceedsLimit = false;
        try
        {
            var info = new FileInfo(path);
            fileLength = info.Length;
            exceedsLimit = fileLength > MaxSettingsFileBytes;
            return true;
        }
        catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
        {
            return false;
        }
    }

    private void EnsureInputSizeWithinLimit(string operation)
    {
        if (!TryValidateInputSize(_path, out var fileLength, out var exceedsLimit))
        {
            throw new IOException(
                $"Settings JSON size validation failed during {operation}. path={_path}.");
        }

        if (!exceedsLimit)
        {
            return;
        }

        var currentRejectCount = Interlocked.Increment(ref _oversizedSettingsRejectCount);
        Debug.WriteLine(
            $"[JsonSettingsDocumentStoreAdapter] oversized-settings-rejected operation={operation} path={_path} length={fileLength} max={MaxSettingsFileBytes} rejectCount={currentRejectCount}");
        throw CreateSettingsFileTooLargeException(operation, fileLength);
    }

    private InvalidDataException CreateSettingsFileTooLargeException(string operation, long fileLength)
    {
        return new InvalidDataException(
            $"Settings JSON exceeds size limit during {operation}. path={_path} length={fileLength} max={MaxSettingsFileBytes}.");
    }

}
