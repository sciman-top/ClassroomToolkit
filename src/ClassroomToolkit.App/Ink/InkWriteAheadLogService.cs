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
    private const string AcknowledgementFileName = ".ink-wal-ack.json";
    private const int FlushDelayMilliseconds = 400;
    private static readonly ConcurrentDictionary<string, object> WalFileLocks = new(StringComparer.OrdinalIgnoreCase);

    private readonly JsonSerializerOptions _options;
    private readonly System.Threading.Timer _flushTimer;
    private readonly object _pendingGate = new();
    private readonly Dictionary<string, Dictionary<string, InkWalEntry?>> _pendingByWalPath = new(StringComparer.OrdinalIgnoreCase);
    private int _flushScheduled;
    private int _disposed;

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
            if (Volatile.Read(ref _disposed) != 0)
            {
                return;
            }

            var normalizedStrokes = InkPayloadNormalizer.NormalizeStrokes(strokes?.ToList());
            GetOrAddPending(walPath)[BuildKey(sourcePath, pageIndex)] = new InkWalEntry
            {
                SourcePath = sourcePath,
                PageIndex = pageIndex,
                Hash = hash ?? string.Empty,
                UpdatedAt = DateTime.UtcNow,
                Strokes = normalizedStrokes
            };
            ScheduleFlush();
        }
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
            if (Volatile.Read(ref _disposed) != 0)
            {
                return;
            }

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
            // 读失败（如文件被占用）按“本次不恢复”处理：WAL 条目保留在磁盘，
            // 由下一次启动的重试继续，绝不能把读失败当空文件把 WAL 清掉。
            if (!TryLoadMap(walPath, out var map))
            {
                return 0;
            }

            if (map.Count == 0)
            {
                return 0;
            }

            // An acknowledgement is written only after a sidecar has been saved but
            // the WAL cannot be replaced/deleted (for example, a reader denied
            // FileShare.Delete). It prevents a later process from replaying that
            // already-applied entry over newer ink.
            if (!TryLoadAcknowledgements(walPath, out var acknowledgements))
            {
                return 0;
            }

            var recovered = 0;
            var keysToRemove = new List<string>();
            var removedEntries = new Dictionary<string, InkWalEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in map)
            {
                var entry = pair.Value;
                if (entry == null || string.IsNullOrWhiteSpace(entry.SourcePath) || entry.PageIndex <= 0)
                {
                    keysToRemove.Add(pair.Key);
                    continue;
                }

                if (IsAcknowledged(acknowledgements, pair.Key, entry))
                {
                    keysToRemove.Add(pair.Key);
                    removedEntries[pair.Key] = entry;
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
                    var strokes = InkPayloadNormalizer.NormalizeStrokes(entry.Strokes);
                    persistence.SaveInkForFile(entry.SourcePath, entry.PageIndex, strokes.ToList());
                    var persisted = persistence.LoadInkPageForFile(entry.SourcePath, entry.PageIndex) ?? new List<InkStrokeData>();
                    if (string.Equals(hashProvider(strokes), hashProvider(persisted), StringComparison.Ordinal))
                    {
                        keysToRemove.Add(pair.Key);
                        removedEntries[pair.Key] = entry;
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
            if (SaveMap(walPath, map))
            {
                RemoveAcknowledgements(walPath, removedEntries);
            }
            else
            {
                PersistAcknowledgements(walPath, removedEntries);
            }
            return recovered;
        }
    }

    /// <summary>
    /// 把内存中尚未落盘的 pending 条目合并写入 WAL 文件。可在任意线程调用，幂等。
    /// </summary>
    public void FlushPending()
    {
        lock (_pendingGate)
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                return;
            }

            // 单次遍历：合并失败的路径（如 WAL 文件被占用）保留 pending，由结尾的
            // 重新调度按防抖间隔延后重试，不做紧密循环。整个遍历持有生命周期锁，
            // 使 Dispose 不会在刷盘尾部与新的调度交错。
            var paths = _pendingByWalPath.Keys.ToArray();
            foreach (var walPath in paths)
            {
                MergePendingToDiskCore(walPath);
            }

            // 合并失败的路径也会在这里获得下一次重试机会。由于整个过程持锁，
            // 不再存在“清标志与新 Upsert 交错”而漏调度的窗口。
            Volatile.Write(ref _flushScheduled, 0);
            if (_pendingByWalPath.Count > 0)
            {
                ScheduleFlush();
            }
        }
    }

    public void Dispose()
    {
        lock (_pendingGate)
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                return;
            }

            FlushPending();
            Volatile.Write(ref _disposed, 1);
        }

        _flushTimer.Dispose();
        GC.SuppressFinalize(this);
    }

    private void ScheduleFlush()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        if (Interlocked.Exchange(ref _flushScheduled, 1) == 1)
        {
            return;
        }

        _flushTimer.Change(FlushDelayMilliseconds, Timeout.Infinite);
    }

    /// <summary>
    /// 把单个 WAL 路径的 pending 合并写入磁盘。调用方必须持有 _pendingGate。
    /// 只有加载、合并与落盘全部成功才丢弃 pending；任一步失败都保留 pending
    /// 等待延后重试——否则 tombstone 被内存丢弃、旧 WAL 残留磁盘，异常退出后的
    /// 恢复流程会把已持久化覆盖/清除的旧墨迹重新回放。
    /// </summary>
    private void MergePendingToDiskCore(string walPath)
    {
        if (!_pendingByWalPath.TryGetValue(walPath, out var pending))
        {
            return;
        }

        bool merged;
        var removedEntries = new Dictionary<string, InkWalEntry>(StringComparer.OrdinalIgnoreCase);
        try
        {
            // Multiple overlay instances can target the same document directory.
            // Keep their read-modify-write cycles under the shared path gate so a
            // later writer cannot erase entries read by an earlier writer.
            lock (GetWalFileLock(walPath))
            {
                merged = TryLoadMap(walPath, out var map);
                if (merged)
                {
                    foreach (var pair in pending)
                    {
                        if (pair.Value == null)
                        {
                            if (map.TryGetValue(pair.Key, out var existing) && existing != null)
                            {
                                removedEntries[pair.Key] = existing;
                            }
                            map.Remove(pair.Key);
                        }
                        else
                        {
                            map[pair.Key] = pair.Value;
                        }
                    }

                    merged = SaveMap(walPath, map);
                    if (merged)
                    {
                        RemoveAcknowledgements(walPath, pending.Keys);
                    }
                    else
                    {
                        PersistAcknowledgements(walPath, removedEntries);
                    }
                }
            }
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine($"[InkWAL] merge pending failed walPath={walPath} ex={ex.GetType().Name} msg={ex.Message}");
            merged = false;
        }

        if (merged)
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

    private static string GetAcknowledgementPath(string walPath)
    {
        return Path.Combine(
            Path.GetDirectoryName(walPath) ?? string.Empty,
            AcknowledgementFileName);
    }

    private static object GetWalFileLock(string walPath)
    {
        return WalFileLocks.GetOrAdd(walPath, static _ => new object());
    }

    /// <summary>
    /// 尝试加载 WAL 映射。文件不存在视为成功（空映射）；读取或解析失败返回 false，
    /// 由调用方决定保留待重试，而不是把读失败当成空文件覆盖磁盘内容。
    /// </summary>
    private bool TryLoadMap(string walPath, out Dictionary<string, InkWalEntry> map)
    {
        if (!File.Exists(walPath))
        {
            map = new Dictionary<string, InkWalEntry>(StringComparer.OrdinalIgnoreCase);
            return true;
        }

        try
        {
            var json = File.ReadAllText(walPath);
            var parsed = JsonSerializer.Deserialize<Dictionary<string, InkWalEntry>>(json, _options);
            map = parsed != null
                ? new Dictionary<string, InkWalEntry>(parsed, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, InkWalEntry>(StringComparer.OrdinalIgnoreCase);
            return true;
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine($"[InkWAL] failed to load wal path={walPath} ex={ex.GetType().Name} msg={ex.Message}");
            map = new Dictionary<string, InkWalEntry>(StringComparer.OrdinalIgnoreCase);
            return false;
        }
    }

    private bool SaveMap(string walPath, Dictionary<string, InkWalEntry> map)
    {
        try
        {
            if (map.Count == 0)
            {
                if (File.Exists(walPath))
                {
                    File.Delete(walPath);
                }
                return true;
            }

            var json = JsonSerializer.Serialize(map, _options);
            InkAtomicFileWriter.WriteAllText(walPath, json, "[InkWAL]");
            return true;
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine($"[InkWAL] save failed walPath={walPath} ex={ex.GetType().Name} msg={ex.Message}");
            return false;
        }
    }

    private bool TryLoadAcknowledgements(
        string walPath,
        out Dictionary<string, InkWalAcknowledgement> acknowledgements)
    {
        var acknowledgementPath = GetAcknowledgementPath(walPath);
        if (!File.Exists(acknowledgementPath))
        {
            acknowledgements = new Dictionary<string, InkWalAcknowledgement>(StringComparer.OrdinalIgnoreCase);
            return true;
        }

        try
        {
            var json = File.ReadAllText(acknowledgementPath);
            var parsed = JsonSerializer.Deserialize<Dictionary<string, InkWalAcknowledgement?>>(json, _options);
            acknowledgements = new Dictionary<string, InkWalAcknowledgement>(StringComparer.OrdinalIgnoreCase);
            if (parsed != null)
            {
                foreach (var pair in parsed)
                {
                    if (pair.Value != null)
                    {
                        acknowledgements[pair.Key] = pair.Value;
                    }
                }
            }
            return true;
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine($"[InkWAL] failed to load acknowledgement path={acknowledgementPath} ex={ex.GetType().Name} msg={ex.Message}");
            acknowledgements = new Dictionary<string, InkWalAcknowledgement>(StringComparer.OrdinalIgnoreCase);
            return false;
        }
    }

    private bool SaveAcknowledgements(
        string walPath,
        Dictionary<string, InkWalAcknowledgement> acknowledgements)
    {
        var acknowledgementPath = GetAcknowledgementPath(walPath);
        try
        {
            if (acknowledgements.Count == 0)
            {
                if (File.Exists(acknowledgementPath))
                {
                    File.Delete(acknowledgementPath);
                }
                return true;
            }

            var json = JsonSerializer.Serialize(acknowledgements, _options);
            InkAtomicFileWriter.WriteAllText(acknowledgementPath, json, "[InkWAL]");
            return true;
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine($"[InkWAL] failed to save acknowledgement path={acknowledgementPath} ex={ex.GetType().Name} msg={ex.Message}");
            return false;
        }
    }

    private static bool IsAcknowledged(
        IReadOnlyDictionary<string, InkWalAcknowledgement> acknowledgements,
        string key,
        InkWalEntry entry)
    {
        return acknowledgements.TryGetValue(key, out var acknowledgement)
            && string.Equals(acknowledgement.Hash, entry.Hash, StringComparison.Ordinal)
            && acknowledgement.UpdatedAt == entry.UpdatedAt;
    }

    private void PersistAcknowledgements(
        string walPath,
        IReadOnlyDictionary<string, InkWalEntry> entries)
    {
        if (entries.Count == 0 || !TryLoadAcknowledgements(walPath, out var acknowledgements))
        {
            return;
        }

        foreach (var pair in entries)
        {
            acknowledgements[pair.Key] = new InkWalAcknowledgement
            {
                Hash = pair.Value.Hash,
                UpdatedAt = pair.Value.UpdatedAt
            };
        }

        _ = SaveAcknowledgements(walPath, acknowledgements);
    }

    private void RemoveAcknowledgements(
        string walPath,
        IReadOnlyDictionary<string, InkWalEntry> entries)
    {
        RemoveAcknowledgements(walPath, entries.Keys);
    }

    private void RemoveAcknowledgements(string walPath, IEnumerable<string> keys)
    {
        if (!TryLoadAcknowledgements(walPath, out var acknowledgements))
        {
            return;
        }

        var removed = false;
        foreach (var key in keys)
        {
            removed |= acknowledgements.Remove(key);
        }

        if (removed || acknowledgements.Count == 0)
        {
            _ = SaveAcknowledgements(walPath, acknowledgements);
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

    private sealed class InkWalAcknowledgement
    {
        public string Hash { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }
    }
}
