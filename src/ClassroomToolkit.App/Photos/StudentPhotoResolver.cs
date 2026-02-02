using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;

namespace ClassroomToolkit.App.Photos;

public sealed class StudentPhotoResolver
{
    private static readonly string[] Extensions = { ".jpg", ".jpeg", ".png", ".bmp" };
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);  // 延长缓存时间，减少重复索引
    private readonly string _rootPath;
    private readonly ConcurrentDictionary<string, DirectoryCache> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, object> _indexLocks = new(StringComparer.OrdinalIgnoreCase);

    public StudentPhotoResolver(string rootPath)
    {
        _rootPath = rootPath;
    }

    /// <summary>
    /// 在后台预热照片索引，提升首次查询性能
    /// </summary>
    public void WarmupCache(IEnumerable<string>? classNames = null)
    {
        _ = Task.Run(() =>
        {
            try
            {
                if (classNames != null)
                {
                    foreach (var className in classNames)
                    {
                        var normalizedClass = SanitizeSegment(className);
                        if (!string.IsNullOrWhiteSpace(normalizedClass))
                        {
                            var directory = Path.Combine(_rootPath, normalizedClass);
                            if (Directory.Exists(directory))
                            {
                                GetIndex(directory);  // 触发索引构建
                            }
                        }
                    }
                }
                else if (Directory.Exists(_rootPath))
                {
                    // 预热所有班级目录
                    foreach (var directory in Directory.EnumerateDirectories(_rootPath))
                    {
                        GetIndex(directory);
                    }
                }
            }
            catch
            {
                // 静默失败，不影响主流程
            }
        });
    }

    public string? ResolvePhotoPath(string className, string studentId)
    {
        if (string.IsNullOrWhiteSpace(studentId))
        {
            return null;
        }
        var normalizedClass = SanitizeSegment(className);
        if (!string.Equals(normalizedClass, (className ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase))
        {
            Debug.WriteLine($"StudentPhotoResolver: 班级名包含非法字符或空值，已规范化为 '{normalizedClass}'。");
        }
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

    public void InvalidateStudentCache(string className, string studentId)
    {
        if (string.IsNullOrWhiteSpace(studentId))
        {
            return;
        }
        var normalizedClass = SanitizeSegment(className);
        if (string.IsNullOrWhiteSpace(normalizedClass))
        {
            normalizedClass = "default";
        }
        var directory = Path.Combine(_rootPath, normalizedClass);
        _cache.TryRemove(directory, out _);
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
        lock (GetIndexLock(directory))
        {
            now = DateTime.UtcNow;
            if (_cache.TryGetValue(directory, out cached) && now - cached.Timestamp < CacheTtl)
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
    }

    private object GetIndexLock(string directory)
    {
        return _indexLocks.GetOrAdd(directory, _ => new object());
    }

    private sealed record DirectoryCache(DateTime Timestamp, Dictionary<string, string> Index);
}
