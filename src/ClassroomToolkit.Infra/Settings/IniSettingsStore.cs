using System.Text;
using System.Diagnostics;
using ClassroomToolkit.Domain.Utilities;

namespace ClassroomToolkit.Infra.Settings;

public sealed class IniSettingsStore
{
    private const long MaxIniFileBytes = 4L * 1024 * 1024;
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
        if (!TryValidateInputSize(_path, out var fileLength))
        {
            Debug.WriteLine($"[IniSettingsStore] load failed path={_path} reason=size-check-failed length={fileLength}");
            return false;
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
            Debug.WriteLine($"[IniSettingsStore] read bytes failed path={path}");
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
            Debug.WriteLine($"[IniSettingsStore] decode failed encoding={encoding.WebName} ex={ex.GetType().Name}");
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
            Debug.WriteLine($"[IniSettingsStore] GB18030 encoding unavailable: {ex.Message}");
            return null;
        }
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
