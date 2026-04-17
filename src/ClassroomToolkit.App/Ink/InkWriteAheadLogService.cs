using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClassroomToolkit.Domain.Utilities;

namespace ClassroomToolkit.App.Ink;

/// <summary>
/// Minimal write-ahead log for in-session ink snapshots.
/// Used to recover unsaved page edits after abnormal process termination.
/// </summary>
public sealed class InkWriteAheadLogService
{
    private const string InkFolderName = ".ctk-ink";
    private const string WalFileName = ".ink-wal.json";
    private static readonly object FileLock = new();

    private readonly JsonSerializerOptions _options;

    public InkWriteAheadLogService()
    {
        _options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
        _options.Converters.Add(new JsonStringEnumConverter());
    }

    public void Upsert(string sourcePath, int pageIndex, IReadOnlyList<InkStrokeData> strokes, string hash)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || pageIndex <= 0)
        {
            return;
        }

        var walPath = GetWalPath(sourcePath);
        lock (FileLock)
        {
            var map = LoadMap(walPath);
            map[BuildKey(sourcePath, pageIndex)] = new InkWalEntry
            {
                SourcePath = sourcePath,
                PageIndex = pageIndex,
                Hash = hash ?? string.Empty,
                UpdatedAt = DateTime.UtcNow,
                Strokes = strokes?.ToList() ?? new List<InkStrokeData>()
            };
            SaveMap(walPath, map);
        }
    }

    public void Remove(string sourcePath, int pageIndex)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || pageIndex <= 0)
        {
            return;
        }

        var walPath = GetWalPath(sourcePath);
        lock (FileLock)
        {
            var map = LoadMap(walPath);
            if (!map.Remove(BuildKey(sourcePath, pageIndex)))
            {
                return;
            }
            SaveMap(walPath, map);
        }
    }

    public int RecoverDirectory(string directoryPath, InkPersistenceService persistence, Func<IReadOnlyList<InkStrokeData>, string> hashProvider)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) || persistence == null || hashProvider == null)
        {
            return 0;
        }

        var walPath = GetWalPathInDirectory(directoryPath);
        lock (FileLock)
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
        string? temp = null;
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

            var directory = Path.GetDirectoryName(walPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(map, _options);
            temp = $"{walPath}.{Guid.NewGuid():N}.tmp";
            File.WriteAllText(temp, json);
            if (File.Exists(walPath))
            {
                TryReplaceOrOverwrite(temp, walPath);
            }
            else
            {
                File.Move(temp, walPath);
            }
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine($"[InkWAL] save failed walPath={walPath} ex={ex.GetType().Name} msg={ex.Message}");
            // Ignore WAL persistence errors.
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(temp) && File.Exists(temp))
            {
                try
                {
                    File.Delete(temp);
                }
                catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
                {
                    Debug.WriteLine($"[InkWAL] temp cleanup failed path={temp} ex={ex.GetType().Name} msg={ex.Message}");
                    // Best-effort cleanup for temp WAL files.
                }
            }
        }
    }

    private static void TryReplaceOrOverwrite(string tempPath, string targetPath)
    {
        try
        {
            File.Replace(tempPath, targetPath, null);
        }
        catch (Exception ex) when (AtomicReplaceFallbackPolicy.ShouldFallback(ex))
        {
            File.Copy(tempPath, targetPath, overwrite: true);
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
