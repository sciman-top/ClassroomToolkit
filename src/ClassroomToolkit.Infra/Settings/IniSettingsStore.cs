using System.Text;
using System.Diagnostics;
using ClassroomToolkit.Domain.Utilities;

namespace ClassroomToolkit.Infra.Settings;

public sealed class IniSettingsStore
{
    private readonly string _path;

    public IniSettingsStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
    }

    public string Path => _path;

    public Dictionary<string, Dictionary<string, string>> Load()
    {
        TryLoad(out var data);
        return data;
    }

    public bool TryLoad(out Dictionary<string, Dictionary<string, string>> data)
    {
        data = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(_path))
        {
            return true;
        }
        if (!TryReadAllLinesWithFallback(_path, out var lines))
        {
            Debug.WriteLine($"[IniSettingsStore] load failed path={_path} reason=read-all-lines-fallback-failed");
            return false;
        }
        if (ContainsNullCharacter(lines))
        {
            Debug.WriteLine($"[IniSettingsStore] load failed path={_path} reason=null-character-detected");
            return false;
        }

        string? currentSection = null;
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }
            if (line.StartsWith('#') || line.StartsWith(';'))
            {
                continue;
            }
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                currentSection = line.Substring(1, line.Length - 2).Trim();
                if (!data.ContainsKey(currentSection))
                {
                    data[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }
                continue;
            }
            if (currentSection == null)
            {
                continue;
            }
            var separatorIndex = line.IndexOf('=');
            if (separatorIndex < 0)
            {
                separatorIndex = line.IndexOf(':');
            }
            if (separatorIndex <= 0)
            {
                continue;
            }
            var key = line.Substring(0, separatorIndex).Trim();
            var value = line.Substring(separatorIndex + 1).Trim();
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }
            data[currentSection][key] = value;
        }
        return true;
    }

    private static bool ContainsNullCharacter(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            if (line.Contains('\0'))
            {
                return true;
            }
        }
        return false;
    }

    private static bool TryReadAllLinesWithFallback(string path, out string[] lines)
    {
        static bool TryRead(string sourcePath, Encoding encoding, out string[] content)
        {
            try
            {
                content = File.ReadAllLines(sourcePath, encoding);
                return true;
            }
            catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
            {
                content = Array.Empty<string>();
                return false;
            }
        }

        var utf8Strict = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        if (TryRead(path, utf8Strict, out lines))
        {
            return true;
        }
        if (TryRead(path, Encoding.Unicode, out lines))
        {
            return true;
        }
        if (TryRead(path, Encoding.BigEndianUnicode, out lines))
        {
            return true;
        }
        return TryRead(path, Encoding.Default, out lines);
    }

    public void Save(Dictionary<string, Dictionary<string, string>> data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var builder = new StringBuilder();
        foreach (var section in data)
        {
            builder.Append('[').Append(section.Key).Append(']').AppendLine();
            var sectionData = section.Value ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in sectionData)
            {
                builder.Append(pair.Key).Append('=').Append(pair.Value).AppendLine();
            }
            builder.AppendLine();
        }
        var directory = System.IO.Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
        AtomicFileReplaceUtility.WriteAtomically(
            _path,
            tempPath => File.WriteAllText(tempPath, builder.ToString(), Encoding.UTF8),
            onTempCleanupFailure: static (tempPath, ex) =>
            {
                if (!InfraExceptionFilterPolicy.IsNonFatal(ex))
                {
                    return;
                }

                Debug.WriteLine(
                    $"[IniSettingsStore] temp cleanup failed path={tempPath} ex={ex.GetType().Name} msg={ex.Message}");
            });
    }

}
