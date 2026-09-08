using System.Text;
using ClassroomToolkit.Domain.Utilities;

using ClassroomToolkit.Infra.Logging;

namespace ClassroomToolkit.Infra.Settings;

public sealed class IniSettingsStore
{
    private const long MaxIniFileBytes = 4L * 1024 * 1024;
    private readonly string _path;
    private string[]? _loadedLines;

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
        _loadedLines = null;
        if (!File.Exists(_path))
        {
            return true;
        }
        if (!TryValidateInputSize(_path, out var fileLength))
        {
            InfraDiagnosticsLog.Write($"[IniSettingsStore] load failed path={_path} reason=size-check-failed length={fileLength}");
            return false;
        }
        if (!TryReadAllLinesWithFallback(_path, out var lines))
        {
            InfraDiagnosticsLog.Write($"[IniSettingsStore] load failed path={_path} reason=read-all-lines-fallback-failed");
            return false;
        }
        if (ContainsNullCharacter(lines))
        {
            InfraDiagnosticsLog.Write($"[IniSettingsStore] load failed path={_path} reason=null-character-detected");
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
            var separatorIndex = line.IndexOf('=', StringComparison.Ordinal);
            if (separatorIndex < 0)
            {
                separatorIndex = line.IndexOf(':', StringComparison.Ordinal);
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
        _loadedLines = lines;
        return true;
    }

    private static bool ContainsNullCharacter(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            if (line.Contains('\0', StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static bool TryValidateInputSize(string path, out long fileLength)
    {
        fileLength = 0;
        try
        {
            var info = new FileInfo(path);
            fileLength = info.Length;
            return fileLength <= MaxIniFileBytes;
        }
        catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
        {
            return false;
        }
    }

    private static bool TryReadAllLinesWithFallback(string path, out string[] lines)
    {
        lines = Array.Empty<string>();
        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(path);
        }
        catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
        {
            InfraDiagnosticsLog.Write($"[IniSettingsStore] read bytes failed path={path}");
            return false;
        }

        // 解码顺序：BOM 显式判定 → UTF-8 严格（现代/ASCII 文件）→ GB18030（旧版中文 ANSI）→
        // UTF-8 宽松兜底。不能直接用 Encoding.Unicode 裸解：ANSI 字节按 UTF-16 解码几乎
        // 永不抛错，会把旧版 GBK 文件“成功”读成乱码并覆盖用户设置。
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return TryDecodeLines(bytes, Encoding.Unicode, out lines);
        }
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            return TryDecodeLines(bytes, Encoding.BigEndianUnicode, out lines);
        }
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return TryDecodeLines(bytes, Encoding.UTF8, out lines);
        }

        var utf8Strict = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        if (TryDecodeLines(bytes, utf8Strict, out lines))
        {
            return true;
        }

        var legacyAnsi = TryGetLegacyChineseEncoding();
        if (legacyAnsi != null && TryDecodeLines(bytes, legacyAnsi, out lines))
        {
            return true;
        }

        return TryDecodeLines(bytes, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), out lines);
    }

    private static bool TryDecodeLines(byte[] bytes, Encoding encoding, out string[] lines)
    {
        lines = Array.Empty<string>();
        try
        {
            using var reader = new StreamReader(new MemoryStream(bytes), encoding);
            var result = new List<string>(bytes.Length / 32 + 1);
            while (reader.ReadLine() is { } line)
            {
                result.Add(line);
            }

            lines = result.ToArray();
            return true;
        }
        catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
        {
            InfraDiagnosticsLog.Write($"[IniSettingsStore] decode failed encoding={encoding.WebName} ex={ex.GetType().Name}");
            return false;
        }
    }

    private static Encoding? TryGetLegacyChineseEncoding()
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding("GB18030");
        }
        catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
        {
            InfraDiagnosticsLog.Write($"[IniSettingsStore] GB18030 encoding unavailable: {ex.Message}");
            return null;
        }
    }

    // INI 行结构不支持值内换行；值来自自由文本字段（路径列表、JSON 片段等）时
    // 剥离 CR/LF，避免读回被截断并随下次保存固化。
    private static string SanitizeIniValue(string value)
    {
        if (string.IsNullOrEmpty(value)
            || !(value.Contains('\r', StringComparison.Ordinal) || value.Contains('\n', StringComparison.Ordinal)))
        {
            return value;
        }

        return value
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal);
    }

    public void Save(Dictionary<string, Dictionary<string, string>> data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var lines = BuildPreservingLines(data);
        var builder = new StringBuilder();
        foreach (var line in lines)
        {
            builder.AppendLine(line);
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

                InfraDiagnosticsLog.Write(
                    $"[IniSettingsStore] temp cleanup failed path={tempPath} ex={ex.GetType().Name} msg={ex.Message}");
            });
        _loadedLines = lines.ToArray();
    }

    private List<string> BuildPreservingLines(Dictionary<string, Dictionary<string, string>> data)
    {
        if (_loadedLines == null)
        {
            return BuildFreshLines(data);
        }

        var result = new List<string>(_loadedLines.Length + data.Count * 2);
        var seenKeys = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var currentSection = (string?)null;

        foreach (var rawLine in _loadedLines)
        {
            var trimmed = rawLine.Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                AppendMissingKeys(result, currentSection, data, seenKeys);
                currentSection = trimmed.Substring(1, trimmed.Length - 2).Trim();
                result.Add(rawLine);
                continue;
            }

            if (currentSection != null && TryParseKeyValue(trimmed, out var key, out _)
                && TryGetSection(data, currentSection, out var sectionData))
            {
                if (!TryGetValue(sectionData, key, out var replacement))
                {
                    // Parsed keys are exposed by Load; their absence means the
                    // caller intentionally removed them. Malformed lines do not
                    // enter this branch and remain byte-for-byte preserved.
                    continue;
                }
                var separatorIndex = FindSeparatorIndex(rawLine);
                result.Add(string.Concat(rawLine.AsSpan(0, separatorIndex + 1), SanitizeIniValue(replacement)));
                MarkSeen(seenKeys, currentSection, key);
                continue;
            }

            result.Add(rawLine);
        }

        AppendMissingKeys(result, currentSection, data, seenKeys);
        foreach (var section in data)
        {
            if (!ContainsSection(_loadedLines, section.Key))
            {
                if (result.Count > 0 && !string.IsNullOrWhiteSpace(result[^1]))
                {
                    result.Add(string.Empty);
                }
                result.Add("[" + section.Key + "]");
                foreach (var pair in section.Value ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase))
                {
                    result.Add(pair.Key + "=" + SanitizeIniValue(pair.Value));
                }
            }
        }

        return result;
    }

    private static List<string> BuildFreshLines(Dictionary<string, Dictionary<string, string>> data)
    {
        var lines = new List<string>();
        foreach (var section in data)
        {
            lines.Add("[" + section.Key + "]");
            foreach (var pair in section.Value ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase))
            {
                lines.Add(pair.Key + "=" + SanitizeIniValue(pair.Value));
            }
            lines.Add(string.Empty);
        }
        return lines;
    }

    private static void AppendMissingKeys(
        List<string> result,
        string? sectionName,
        Dictionary<string, Dictionary<string, string>> data,
        Dictionary<string, HashSet<string>> seenKeys)
    {
        if (sectionName == null || !TryGetSection(data, sectionName, out var sectionData))
        {
            return;
        }

        var seen = seenKeys.TryGetValue(sectionName, out var keys)
            ? keys
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in sectionData ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase))
        {
            if (seen.Contains(pair.Key))
            {
                continue;
            }
            result.Add(pair.Key + "=" + SanitizeIniValue(pair.Value));
            seen.Add(pair.Key);
        }
        seenKeys[sectionName] = seen;
    }

    private static bool TryParseKeyValue(string line, out string key, out string value)
    {
        key = string.Empty;
        value = string.Empty;
        var separatorIndex = line.IndexOf('=', StringComparison.Ordinal);
        if (separatorIndex < 0)
        {
            separatorIndex = line.IndexOf(':', StringComparison.Ordinal);
        }
        if (separatorIndex <= 0)
        {
            return false;
        }
        key = line.Substring(0, separatorIndex).Trim();
        value = line.Substring(separatorIndex + 1).Trim();
        return key.Length > 0;
    }

    private static int FindSeparatorIndex(string line)
    {
        var equals = line.IndexOf('=', StringComparison.Ordinal);
        var colon = line.IndexOf(':', StringComparison.Ordinal);
        if (equals < 0) return colon;
        if (colon < 0) return equals;
        return Math.Min(equals, colon);
    }

    private static bool TryGetSection(
        Dictionary<string, Dictionary<string, string>> data,
        string sectionName,
        out Dictionary<string, string>? section)
    {
        if (data.TryGetValue(sectionName, out section))
        {
            return true;
        }
        section = null;
        return false;
    }

    private static bool TryGetValue(Dictionary<string, string>? section, string key, out string value)
    {
        if (section != null && section.TryGetValue(key, out value!))
        {
            return true;
        }
        value = string.Empty;
        return false;
    }

    private static void MarkSeen(Dictionary<string, HashSet<string>> seenKeys, string section, string key)
    {
        if (!seenKeys.TryGetValue(section, out var keys))
        {
            keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            seenKeys[section] = keys;
        }
        keys.Add(key);
    }

    private static bool ContainsSection(IEnumerable<string> lines, string sectionName)
    {
        var header = "[" + sectionName + "]";
        return lines.Any(line => string.Equals(line.Trim(), header, StringComparison.OrdinalIgnoreCase));
    }

}
