using System.Collections.Concurrent;
using System.IO;
using System.Linq;

namespace ClassroomToolkit.App.Photos;

public sealed class StudentPhotoResolver
{
    private static readonly string[] Extensions = { ".jpg", ".jpeg", ".png", ".bmp" };
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(8);
    private readonly string _rootPath;
    private readonly ConcurrentDictionary<string, DirectoryCache> _cache = new(StringComparer.OrdinalIgnoreCase);

    public StudentPhotoResolver(string rootPath)
    {
        _rootPath = rootPath;
    }

    public string? ResolvePhotoPath(string className, string studentId)
    {
        if (string.IsNullOrWhiteSpace(studentId))
        {
            return null;
        }
        var normalizedClass = SanitizeSegment(className);
        if (string.IsNullOrWhiteSpace(normalizedClass))
        {
            normalizedClass = "default";
        }
        var directory = Path.Combine(_rootPath, normalizedClass);
        if (!Directory.Exists(directory))
        {
            return null;
        }
        foreach (var ext in Extensions)
        {
            var candidate = Path.Combine(directory, studentId + ext);
            if (File.Exists(candidate))
            {
                return candidate;
            }
            var upper = Path.Combine(directory, studentId + ext.ToUpperInvariant());
            if (File.Exists(upper))
            {
                return upper;
            }
        }
        var index = GetIndex(directory);
        var key = studentId.Trim().ToLowerInvariant();
        return index.TryGetValue(key, out var path) ? path : null;
    }

    public static string SanitizeSegment(string value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return string.Empty;
        }
        var invalid = Path.GetInvalidFileNameChars();
        var buffer = text.ToCharArray();
        for (var i = 0; i < buffer.Length; i++)
        {
            if (invalid.Contains(buffer[i]))
            {
                buffer[i] = '_';
            }
        }
        return new string(buffer).Trim('_');
    }

    private Dictionary<string, string> GetIndex(string directory)
    {
        var now = DateTime.UtcNow;
        if (_cache.TryGetValue(directory, out var cached) && now - cached.Timestamp < CacheTtl)
        {
            return cached.Index;
        }
        var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var file in Directory.EnumerateFiles(directory))
            {
                var ext = Path.GetExtension(file);
                if (!Extensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }
                var baseName = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                if (!index.ContainsKey(baseName))
                {
                    index[baseName] = file;
                }
            }
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        _cache[directory] = new DirectoryCache(now, index);
        return index;
    }

    private sealed record DirectoryCache(DateTime Timestamp, Dictionary<string, string> Index);
}
