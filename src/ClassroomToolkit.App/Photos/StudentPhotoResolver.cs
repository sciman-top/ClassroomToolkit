using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Threading;
using ClassroomToolkit.App.Utilities;

namespace ClassroomToolkit.App.Photos;

public sealed class StudentPhotoResolver : IDisposable
{
    private static readonly string[] PreferredExtensions =
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".bmp"
    };
    private static readonly HashSet<string> Extensions = new(PreferredExtensions, StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<char> InvalidFileNameChars = Path.GetInvalidFileNameChars().ToHashSet();
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);  // 延长缓存时间，减少重复索引
    private static readonly TimeSpan FreshCacheDirectProbeWindow = TimeSpan.FromSeconds(1);
    private static readonly Dictionary<string, string> EmptyIndex = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _rootPath;
    private readonly ConcurrentDictionary<string, DirectoryCache> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, object> _indexLocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _warmupLock = new();
    private static readonly EnumerationOptions TopLevelIgnoreInaccessibleOptions = new()
    {
        RecurseSubdirectories = false,
        IgnoreInaccessible = true
    };
    private CancellationTokenSource? _warmupCancellation;
    private int _disposed;

    public StudentPhotoResolver(string rootPath)
    {
        _rootPath = rootPath;
    }

    /// <summary>
    /// 在后台预热照片索引，提升首次查询性能
    /// </summary>
    public void WarmupCache(IEnumerable<string>? classNames = null)
    {
        if (!TryReplaceWarmupCancellationToken(out var token))
        {
            return;
        }

        _ = SafeTaskRunner.Run("StudentPhotoResolver.WarmupCache", _ =>
        {
            if (classNames != null)
            {
                var visitedClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var className in classNames)
                {
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }
                    var normalizedClass = SanitizeSegment(className);
                    if (!string.IsNullOrWhiteSpace(normalizedClass) && visitedClasses.Add(normalizedClass))
                    {
                        var directory = Path.Combine(_rootPath, normalizedClass);
                        if (Directory.Exists(directory))
                        {
                            GetIndex(directory);  // 触发索引构建
                        }
                    }
                }
                return;
            }

            if (!Directory.Exists(_rootPath))
            {
                return;
            }
            // 预热所有班级目录
            foreach (var directory in Directory.EnumerateDirectories(_rootPath, "*", TopLevelIgnoreInaccessibleOptions))
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }
                GetIndex(directory);
            }
        }, token, ex => Debug.WriteLine($"StudentPhotoResolver warmup failed: {ex.GetType().Name} - {ex.Message}"));
    }

    public string? ResolvePhotoPath(string className, string studentId)
    {
        if (IsDisposed())
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(studentId))
        {
            return null;
        }
        var normalizedStudentId = SanitizeSegment(studentId);
        if (!string.Equals(normalizedStudentId, studentId.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            Debug.WriteLine($"StudentPhotoResolver: 学号包含非法字符或路径片段，已规范化为 '{normalizedStudentId}'。");
        }
        if (string.IsNullOrWhiteSpace(normalizedStudentId))
        {
            return null;
        }
        var normalizedClass = NormalizeClassName(className);
        var directory = Path.Combine(_rootPath, normalizedClass);
        if (!Directory.Exists(directory))
        {
            return null;
        }
        var key = normalizedStudentId;
        if (TryGetFreshIndex(directory, out var freshCache))
        {
            if (freshCache.Index.TryGetValue(key, out var cachedPath))
            {
                return cachedPath;
            }

            // Only probe filesystem when directory timestamp changed; this avoids repeated misses
            // paying 4x File.Exists checks while cache is still fresh.
            if (TryGetDirectoryWriteTimeUtc(directory, out var writeTimeUtc)
                && writeTimeUtc <= freshCache.DirectoryWriteTimeUtc
                && DateTime.UtcNow - freshCache.Timestamp > FreshCacheDirectProbeWindow)
            {
                return null;
            }

            var changedDirectPath = ResolveByPreferredExtensions(directory, normalizedStudentId);
            if (!string.IsNullOrWhiteSpace(changedDirectPath))
            {
                _cache.TryRemove(directory, out _);
            }
            return changedDirectPath;
        }

        var direct = ResolveByPreferredExtensions(directory, normalizedStudentId);
        if (!string.IsNullOrWhiteSpace(direct))
        {
            return direct;
        }

        var index = GetIndex(directory);
        return index.TryGetValue(key, out var path) ? path : null;
    }

    public void InvalidateStudentCache(string className, string studentId)
    {
        if (IsDisposed())
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(studentId))
        {
            return;
        }
        var normalizedClass = NormalizeClassName(className);
        var directory = Path.Combine(_rootPath, normalizedClass);
        _cache.TryRemove(directory, out _);
        _indexLocks.TryRemove(directory, out _);
    }

    public static string SanitizeSegment(string value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return string.Empty;
        }

        var requiresSanitize = false;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (InvalidFileNameChars.Contains(ch) ||
                ch == Path.DirectorySeparatorChar ||
                ch == Path.AltDirectorySeparatorChar)
            {
                requiresSanitize = true;
                break;
            }
        }

        if (!requiresSanitize)
        {
            if (text is "." or "..")
            {
                return string.Empty;
            }

            return text.Trim('_');
        }

        var buffer = text.ToCharArray();
        for (var i = 0; i < buffer.Length; i++)
        {
            if (InvalidFileNameChars.Contains(buffer[i]) ||
                buffer[i] == Path.DirectorySeparatorChar ||
                buffer[i] == Path.AltDirectorySeparatorChar)
            {
                buffer[i] = '_';
            }
        }
        var sanitized = new string(buffer).Trim('_');
        if (sanitized is "." or "..")
        {
            return string.Empty;
        }
        return sanitized;
    }

    private Dictionary<string, string> GetIndex(string directory)
    {
        if (IsDisposed())
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var now = DateTime.UtcNow;
        if (_cache.TryGetValue(directory, out var cached)
            && StudentPhotoCachePolicy.ShouldReuseCache(now, cached.Timestamp, CacheTtl))
        {
            return cached.Index;
        }
        lock (GetIndexLock(directory))
        {
            if (IsDisposed())
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            now = DateTime.UtcNow;
            if (_cache.TryGetValue(directory, out cached)
                && StudentPhotoCachePolicy.ShouldReuseCache(now, cached.Timestamp, CacheTtl))
            {
                return cached.Index;
            }
            var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var file in Directory.EnumerateFiles(directory, "*", TopLevelIgnoreInaccessibleOptions))
                {
                    var ext = Path.GetExtension(file);
                    if (string.IsNullOrWhiteSpace(ext) || !Extensions.Contains(ext))
                    {
                        continue;
                    }
                    var baseName = Path.GetFileNameWithoutExtension(file);
                    index.TryAdd(baseName, file);
                }
            }
            catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            var directoryWriteTimeUtc = GetDirectoryWriteTimeUtcOrDefault(directory);
            if (IsDisposed())
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            _cache[directory] = new DirectoryCache(now, directoryWriteTimeUtc, index);
            return index;
        }
    }

    private bool TryGetFreshIndex(string directory, out DirectoryCache cache)
    {
        var now = DateTime.UtcNow;
        if (_cache.TryGetValue(directory, out var found)
            && StudentPhotoCachePolicy.ShouldReuseCache(now, found.Timestamp, CacheTtl))
        {
            cache = found;
            return true;
        }

        cache = new DirectoryCache(DateTime.MinValue, DateTime.MinValue, EmptyIndex);
        return false;
    }

    private static DateTime GetDirectoryWriteTimeUtcOrDefault(string directory)
    {
        return TryGetDirectoryWriteTimeUtc(directory, out var writeTimeUtc)
            ? writeTimeUtc
            : DateTime.MinValue;
    }

    private static bool TryGetDirectoryWriteTimeUtc(string directory, out DateTime writeTimeUtc)
    {
        writeTimeUtc = DateTime.MinValue;
        try
        {
            writeTimeUtc = Directory.GetLastWriteTimeUtc(directory);
            return true;
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine(
                $"StudentPhotoResolver: 读取目录修改时间失败，directory='{directory}', reason={ex.GetType().Name}:{ex.Message}");
            return false;
        }
    }

    private static string? ResolveByPreferredExtensions(string directory, string normalizedStudentId)
    {
        var filePrefix = Path.Combine(directory, normalizedStudentId);
        foreach (var ext in PreferredExtensions)
        {
            var candidate = filePrefix + ext;
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private object GetIndexLock(string directory)
    {
        return _indexLocks.GetOrAdd(directory, _ => new object());
    }

    private bool TryReplaceWarmupCancellationToken(out CancellationToken token)
    {
        lock (_warmupLock)
        {
            if (IsDisposed())
            {
                token = CancellationToken.None;
                return false;
            }

            _warmupCancellation?.Cancel();
            _warmupCancellation?.Dispose();
            _warmupCancellation = new CancellationTokenSource();
            token = _warmupCancellation.Token;
            return true;
        }
    }

    public void Dispose()
    {
        lock (_warmupLock)
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }
            _warmupCancellation?.Cancel();
            _warmupCancellation?.Dispose();
            _warmupCancellation = null;
        }

        _cache.Clear();
        _indexLocks.Clear();
    }

    private bool IsDisposed()
    {
        return Volatile.Read(ref _disposed) != 0;
    }

    private static string NormalizeClassName(string className)
    {
        var normalizedClass = SanitizeSegment(className);
        if (!string.Equals(normalizedClass, (className ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase))
        {
            Debug.WriteLine($"StudentPhotoResolver: 班级名包含非法字符或空值，已规范化为 '{normalizedClass}'。");
        }
        return string.IsNullOrWhiteSpace(normalizedClass) ? "default" : normalizedClass;
    }

    private sealed record DirectoryCache(DateTime Timestamp, DateTime DirectoryWriteTimeUtc, Dictionary<string, string> Index);
}
