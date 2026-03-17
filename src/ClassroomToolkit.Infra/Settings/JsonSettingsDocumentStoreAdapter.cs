using System.Text;
using System.Text.Json;
using System.Threading;
using ClassroomToolkit.Application.Abstractions;
using ClassroomToolkit.Domain.Utilities;

namespace ClassroomToolkit.Infra.Settings;

public sealed class JsonSettingsDocumentStoreAdapter : ISettingsDocumentStore
{
    private readonly string _path;
    private int _lastLoadSucceeded = 1;

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
                Interlocked.Exchange(ref _lastLoadSucceeded, 1);
                return new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            }

            var json = File.ReadAllText(_path, Encoding.UTF8);
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
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

            Interlocked.Exchange(ref _lastLoadSucceeded, 1);
            return result;
        }
        catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
        {
            Interlocked.Exchange(ref _lastLoadSucceeded, 0);
            return new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public void Save(Dictionary<string, Dictionary<string, string>> data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (File.Exists(_path) && Volatile.Read(ref _lastLoadSucceeded) == 0)
        {
            throw new InvalidOperationException(
                "Settings load previously failed; refusing to overwrite existing JSON settings file.");
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

        var tempPath = $"{_path}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = File.Open(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new Utf8JsonWriter(stream, options))
            {
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
            }

            if (File.Exists(_path))
            {
                TryReplaceOrOverwrite(tempPath, _path);
            }
            else
            {
                File.Move(tempPath, _path);
            }

            Interlocked.Exchange(ref _lastLoadSucceeded, 1);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
                {
                    // Best-effort cleanup; keep the primary save exception.
                }
            }
        }
    }

    private static void TryReplaceOrOverwrite(string tempPath, string targetPath)
    {
        try
        {
            File.Replace(tempPath, targetPath, null);
        }
        catch (Exception ex) when (AtomicReplaceFallbackPolicy.ShouldFallback(ex))
        {
            File.Copy(tempPath, targetPath, overwrite: true);
        }
    }
}
