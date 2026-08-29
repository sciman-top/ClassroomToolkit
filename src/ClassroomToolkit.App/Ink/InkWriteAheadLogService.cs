using System;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace ClassroomToolkit.App.Ink;

/// <summary>
/// Minimal write-ahead log for in-session ink snapshots.
/// Used to recover unsaved page edits after abnormal process termination.
/// </summary>
/// <remarks>
/// 每笔画一次 Upsert 曾直接触发整本 WAL 的读+序列化+原子写，快速书写时 UI 线程每秒
/// 执行多次全量 IO。现在 Upsert 只更新内存 pending 并防抖（≤400ms）合并为一次落盘；
/// Remove（页面已持久化）保持同步，避免残留 WAL 条目在下次会话复活旧墨迹。
/// </remarks>
internal sealed class InkWriteAheadLogService : IDisposable
{
    private const string InkFolderName = ".ctk-ink";
    private const string WalFileName = ".ink-wal.json";
    private const int FlushDelayMilliseconds = 400;
    private static readonly ConcurrentDictionary<string, object> WalFileLocks = new(StringComparer.OrdinalIgnoreCase);

    private readonly JsonSerializerOptions _options;
    private readonly System.Threading.Timer _flushTimer;
    private readonly object _pendingGate = new();
    private readonly Dictionary<string, Dictionary<string, InkWalEntry?>> _pendingByWalPath = new(StringComparer.OrdinalIgnoreCase);
    private int _flushScheduled;

    public InkWriteAheadLogService()
    {
        _options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
        _options.Converters.Add(new JsonStringEnumConverter());
        _flushTimer = new System.Threading.Timer(static state => ((InkWriteAheadLogService)state!).FlushPending(), this, Timeout.Infinite, Timeout.Infinite);
    }

    public void Upsert(string sourcePath, int pageIndex, IReadOnlyList<InkStrokeData> strokes, string hash)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || pageIndex <= 0)
        {
            return;
        }

        var walPath = GetWalPath(sourcePath);
        lock (_pendingGate)
        {
            GetOrAddPending(walPath)[BuildKey(sourcePath, pageIndex)] = new InkWalEntry
            {
                SourcePath = sourcePath,
                PageIndex = pageIndex,
                Hash = hash ?? string.Empty,
                UpdatedAt = DateTime.UtcNow,
                Strokes = strokes?.ToList() ?? new List<InkStrokeData>()
            };
        }

        ScheduleFlush();
    }

    public void Remove(string sourcePath, int pageIndex)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || pageIndex <= 0)
        {
            return;
        }

        var walPath = GetWalPath(sourcePath);
        lock (_pendingGate)
        {
            // tombstone（null）优先于尚未落盘的同键 Upsert，同步合并保证持久化过的页面
            // 不会在 WAL 里留下会在下次会话复活旧墨迹的条目。
            GetOrAddPending(walPath)[BuildKey(sourcePath, pageIndex)] = null;
            MergePendingToDiskCore(walPath);
        }
    }

    public int RecoverDirectory(string directoryPath, InkPersistenceService persistence, Func<IReadOnlyList<InkStrokeData>, string> hashProvider)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) || persistence == null || hashProvider == null)
        {
            return 0;
        }

        FlushPending();

        var walPath = GetWalPathInDirectory(directoryPath);
        lock (GetWalFileLock(walPath))
        {
            var map = LoadMap(walPath);
            if (map.Count == 0)
            {
                return 0;
            }

            var recovered = 0;
            var keysToRemove = new List<string>();
            foreach (var pair in map)
            {
                var entry = pair.Value;
                if (entry == null || string.IsNullOrWhiteSpace(entry.SourcePath) || entry.PageIndex <= 0)
                {
                    keysToRemove.Add(pair.Key);
                    continue;
                }

                if (!string.Equals(
                        Path.GetDirectoryName(entry.SourcePath),
                        directoryPath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!File.Exists(entry.SourcePath))
                {
                    keysToRemove.Add(pair.Key);
                    continue;
                }

                try
                {
                    var strokes = entry.Strokes ?? new List<InkStrokeData>();
                    persistence.SaveInkForFile(entry.SourcePath, entry.PageIndex, strokes.ToList());
                    var persisted = persistence.LoadInkPageForFile(entry.SourcePath, entry.PageIndex) ?? new List<InkStrokeData>();
                    if (string.Equals(hashProvider(strokes), hashProvider(persisted), StringComparison.Ordinal))
                    {
                        keysToRemove.Add(pair.Key);
                        recovered++;
                    }
                }
                catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
                {
                    // Keep WAL entry for next attempt.
                }
            }

            foreach (var key in keysToRemove)
            {
                map.Remove(key);
            }
            SaveMap(walPath, map);
            return recovered;
        }
    }

    /// <summary>
    /// 把内存中尚未落盘的 pending 条目合并写入 WAL 文件。可在任意线程调用，幂等。
    /// </summary>
    public void FlushPending()
    {
        while (true)
        {
            string[] paths;
            lock (_pendingGate)
            {
                if (_pendingByWalPath.Count == 0)
                {
                    break;
                }

                paths = _pendingByWalPath.Keys.ToArray();
            }

            foreach (var walPath in paths)
            {
                lock (_pendingGate)
                {
                    MergePendingToDiskCore(walPath);
                }
            }
        }

        // 先清标志再复查 pending：若在最后一轮合并与清标志之间有新 Upsert 进来，
        // 它的 ScheduleFlush 会因标志仍为 1 而跳过定时器，这里复查避免条目滞留内存。
        Volatile.Write(ref _flushScheduled, 0);
        lock (_pendingGate)
        {
            if (_pendingByWalPath.Count > 0)
            {
                ScheduleFlush();
            }
        }
    }

    public void Dispose()
    {
        FlushPending();
        _flushTimer.Dispose();
        GC.SuppressFinalize(this);
    }

    private void ScheduleFlush()
    {
        if (Interlocked.Exchange(ref _flushScheduled, 1) == 1)
        {
            return;
        }

        _flushTimer.Change(FlushDelayMilliseconds, Timeout.Infinite);
    }

    /// <summary>
    /// 把单个 WAL 路径的 pending 合并写入磁盘。调用方必须持有 _pendingGate。
    /// </summary>
    private void MergePendingToDiskCore(string walPath)
    {
        if (!_pendingByWalPath.TryGetValue(walPath, out var pending))
        {
            return;
        }

        try
        {
            var map = LoadMap(walPath);
            foreach (var pair in pending)
            {
                if (pair.Value == null)
                {
                    map.Remove(pair.Key);
                }
                else
                {
                    map[pair.Key] = pair.Value;
                }
            }

            SaveMap(walPath, map);
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine($"[InkWAL] merge pending failed walPath={walPath} ex={ex.GetType().Name} msg={ex.Message}");
        }
        finally
        {
            _pendingByWalPath.Remove(walPath);
        }
    }

    private Dictionary<string, InkWalEntry?> GetOrAddPending(string walPath)
    {
        if (!_pendingByWalPath.TryGetValue(walPath, out var pending))
        {
            pending = new Dictionary<string, InkWalEntry?>(StringComparer.OrdinalIgnoreCase);
            _pendingByWalPath[walPath] = pending;
        }

        return pending;
    }

    private static string BuildKey(string sourcePath, int pageIndex)
    {
        return $"{sourcePath}|{pageIndex}";
    }

    private static string GetWalPath(string sourcePath)
    {
        var directory = Path.GetDirectoryName(sourcePath) ?? string.Empty;
        return GetWalPathInDirectory(directory);
    }

    private static string GetWalPathInDirectory(string directory)
    {
        return Path.Combine(directory, InkFolderName, WalFileName);
    }

    private static object GetWalFileLock(string walPath)
    {
        return WalFileLocks.GetOrAdd(walPath, static _ => new object());
    }

    private Dictionary<string, InkWalEntry> LoadMap(string walPath)
    {
        if (!File.Exists(walPath))
        {
            return new Dictionary<string, InkWalEntry>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var json = File.ReadAllText(walPath);
            var parsed = JsonSerializer.Deserialize<Dictionary<string, InkWalEntry>>(json, _options);
            return parsed != null
                ? new Dictionary<string, InkWalEntry>(parsed, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, InkWalEntry>(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine($"[InkWAL] failed to load wal path={walPath} ex={ex.GetType().Name} msg={ex.Message}");
            return new Dictionary<string, InkWalEntry>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void SaveMap(string walPath, Dictionary<string, InkWalEntry> map)
    {
        try
        {
            if (map.Count == 0)
            {
                if (File.Exists(walPath))
                {
                    File.Delete(walPath);
                }
                return;
            }

            var json = JsonSerializer.Serialize(map, _options);
            InkAtomicFileWriter.WriteAllText(walPath, json, "[InkWAL]");
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine($"[InkWAL] save failed walPath={walPath} ex={ex.GetType().Name} msg={ex.Message}");
            // Ignore WAL persistence errors.
        }
    }

    private sealed class InkWalEntry
    {
        public string SourcePath { get; set; } = string.Empty;
        public int PageIndex { get; set; }
        public string Hash { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }
        public List<InkStrokeData> Strokes { get; set; } = new();
    }
}
