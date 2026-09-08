using System.Text.Json;
using System.Text.Json.Nodes;
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
    private int _overwriteBlockedAfterLoadFailure;
    private long _lastValidatedWriteTimeUtcTicks = DateTime.MinValue.Ticks;
    private volatile string? _lastValidatedContentHash;
    private JsonObject? _rawRoot;

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
                Interlocked.Exchange(ref _overwriteBlockedAfterLoadFailure, 0);
                Interlocked.Exchange(ref _lastValidatedWriteTimeUtcTicks, DateTime.MinValue.Ticks);
                _lastValidatedContentHash = null;
                _rawRoot = null;
                return new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            }

            EnsureInputSizeWithinLimit(operation: "load");

            using var stream = File.OpenRead(_path);
            using var document = JsonDocument.Parse(stream);
            EnsureObjectRoot(document, operation: "load");
            var rawRoot = JsonNode.Parse(document.RootElement.GetRawText())?.AsObject()
                ?? throw new InvalidDataException("Settings JSON root could not be materialized.");

            var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var sectionNode in document.RootElement.EnumerateObject())
            {
                if (sectionNode.Value.ValueKind != JsonValueKind.Object)
                {
                    // The dictionary API cannot expose a non-object section, but the raw
                    // document is retained so a later settings save does not delete it.
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
            Interlocked.Exchange(ref _overwriteBlockedAfterLoadFailure, 0);
            Interlocked.Exchange(ref _lastValidatedWriteTimeUtcTicks, GetCurrentWriteTimeUtcTicks());
            _lastValidatedContentHash = GetCurrentContentHash();
            _rawRoot = rawRoot;
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
        if (File.Exists(_path) && Volatile.Read(ref _overwriteBlockedAfterLoadFailure) == 1)
        {
            throw new InvalidOperationException(
                "Settings file could not be safely loaded; refusing to overwrite existing JSON settings file.");
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
                BuildMergedRoot(data).WriteTo(writer);
                writer.Flush();
            },
            onTempCleanupFailure: static (tempPath, ex) =>
            {
                Debug.WriteLine(
                    $"[JsonSettingsDocumentStoreAdapter] temp cleanup failed path={tempPath} ex={ex.GetType().Name} msg={ex.Message}");
            });

        Interlocked.Exchange(ref _hasValidatedExistingFileState, 1);
        Interlocked.Exchange(ref _overwriteBlockedAfterLoadFailure, 0);
        Interlocked.Exchange(ref _lastValidatedWriteTimeUtcTicks, GetCurrentWriteTimeUtcTicks());
        _lastValidatedContentHash = GetCurrentContentHash();
        _rawRoot = BuildMergedRoot(data);
    }

    public static long OversizedSettingsRejectCount => Interlocked.Read(ref _oversizedSettingsRejectCount);

    private void EnsureObjectRoot(JsonDocument document, string operation)
    {
        if (document.RootElement.ValueKind == JsonValueKind.Object)
        {
            return;
        }

        throw new InvalidDataException(
            $"Settings JSON root must be an object during {operation}. path={_path} actual={document.RootElement.ValueKind}.");
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
            EnsureObjectRoot(document, operation: "save-preflight");
            _rawRoot = JsonNode.Parse(document.RootElement.GetRawText())?.AsObject()
                ?? throw new InvalidDataException("Settings JSON root could not be materialized during save-preflight.");
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
        Interlocked.Exchange(ref _overwriteBlockedAfterLoadFailure, 1);
        _rawRoot = null;
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

    private JsonObject BuildMergedRoot(Dictionary<string, Dictionary<string, string>> data)
    {
        var root = _rawRoot == null
            ? new JsonObject()
            : JsonNode.Parse(_rawRoot.ToJsonString())?.AsObject()
                ?? new JsonObject();

        foreach (var section in data)
        {
            var rawSectionKey = FindPropertyKey(root, section.Key);
            var rawSection = rawSectionKey != null && root[rawSectionKey] is JsonObject existing
                ? existing
                : new JsonObject();

            MergeSection(rawSection, section.Value);
            if (rawSectionKey == null)
            {
                root[section.Key] = rawSection;
            }
            else if (root[rawSectionKey] is not JsonObject)
            {
                root[rawSectionKey] = rawSection;
            }
        }

        return root;
    }

    private static void MergeSection(JsonObject rawSection, Dictionary<string, string>? sectionData)
    {
        if (sectionData == null)
        {
            rawSection.Clear();
            return;
        }

        foreach (var rawKey in rawSection.Select(pair => pair.Key).ToList())
        {
            if (!sectionData.ContainsKey(rawKey))
            {
                // A key exposed by Load and then removed by the caller is an
                // intentional deletion (legacy migration relies on this).
                rawSection.Remove(rawKey);
            }
        }

        foreach (var item in sectionData)
        {
            var rawKey = FindPropertyKey(rawSection, item.Key);
            if (rawKey != null && IsEquivalentSettingValue(rawSection[rawKey], item.Value))
            {
                // Keep the original JSON node type for unchanged values, including
                // nested objects and arrays that the legacy dictionary API flattens.
                continue;
            }

            rawSection[rawKey ?? item.Key] = JsonValue.Create(item.Value);
        }
    }

    private static string? FindPropertyKey(JsonObject node, string key)
    {
        return node.Select(pair => pair.Key)
            .FirstOrDefault(existing => string.Equals(existing, key, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsEquivalentSettingValue(JsonNode? node, string value)
    {
        if (node == null)
        {
            return string.IsNullOrEmpty(value);
        }

        if (node is JsonValue jsonValue)
        {
            if (jsonValue.TryGetValue<string>(out var stringValue))
            {
                return string.Equals(stringValue, value, StringComparison.Ordinal);
            }
            if (jsonValue.TryGetValue<bool>(out var boolValue))
            {
                return string.Equals(boolValue ? "True" : "False", value, StringComparison.Ordinal);
            }
        }

        if (string.Equals(node.ToJsonString(), value, StringComparison.Ordinal))
        {
            return true;
        }

        try
        {
            var candidate = JsonNode.Parse(value);
            return candidate != null && JsonNode.DeepEquals(node, candidate);
        }
        catch (JsonException)
        {
            return false;
        }
    }

}
