using System.Text;
using System.Text.Json;
using ClassroomToolkit.Application.Abstractions;

namespace ClassroomToolkit.Infra.Settings;

public sealed class JsonSettingsDocumentStoreAdapter : ISettingsDocumentStore
{
    private readonly string _path;

    public JsonSettingsDocumentStoreAdapter(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        _path = path;
    }

    public Dictionary<string, Dictionary<string, string>> Load()
    {
        if (!File.Exists(_path))
        {
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

            return result;
        }
        catch (JsonException)
        {
            return new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        }
        catch (IOException)
        {
            return new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        }
        catch (UnauthorizedAccessException)
        {
            return new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public void Save(Dictionary<string, Dictionary<string, string>> data)
    {
        ArgumentNullException.ThrowIfNull(data);

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
            File.Copy(tempPath, _path, overwrite: true);
            File.Delete(tempPath);
            return;
        }

        File.Move(tempPath, _path);
    }
}
