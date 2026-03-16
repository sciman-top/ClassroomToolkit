using System.Text;
using System.Text.Json;
using System.Threading;
using ClassroomToolkit.Application.Abstractions;

namespace ClassroomToolkit.Infra.Settings;

public sealed class JsonSettingsDocumentStoreAdapter : ISettingsDocumentStore
{
    private readonly string _path;
    private int _lastLoadSucceeded = 1;

    public JsonSettingsDocumentStoreAdapter(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        _path = path;
    }

    public Dictionary<string, Dictionary<string, string>> Load()
    {
        if (!File.Exists(_path))
        {
            Interlocked.Exchange(ref _lastLoadSucceeded, 1);
            return new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
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
        catch (JsonException)
        {
            Interlocked.Exchange(ref _lastLoadSucceeded, 0);
            return new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        }
        catch (IOException)
        {
            Interlocked.Exchange(ref _lastLoadSucceeded, 0);
            return new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        }
        catch (UnauthorizedAccessException)
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
                    foreach (var item in section.Value.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
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
                File.Replace(tempPath, _path, null);
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
                File.Delete(tempPath);
            }
        }
    }
}
