using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClassroomToolkit.App;
using ClassroomToolkit.Domain.Utilities;

namespace ClassroomToolkit.App.Ink;

/// <summary>
/// Method A: Sidecar-based ink persistence.
/// Reads/writes .ink.json files in a hidden .ctk-ink/ folder next to the source file.
/// </summary>
[SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "Ink persistence list APIs match the JSON sidecar model and existing callers; changing them would alter persisted-contract behavior.")]
public sealed class InkPersistenceService
{
    private const string InkFolderName = ".ctk-ink";
    private const string SidecarSuffix = ".ink.json";
    private static readonly EnumerationOptions TopLevelIgnoreInaccessibleOptions = new()
    {
        RecurseSubdirectories = false,
        IgnoreInaccessible = true
    };

    private readonly JsonSerializerOptions _options;
    private readonly object _cacheLock = new();
    private readonly Dictionary<string, CachedInkDocument> _documentCache = new(StringComparer.OrdinalIgnoreCase);

    public InkPersistenceService()
    {
        _options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
        _options.Converters.Add(new JsonStringEnumConverter());
    }

    private sealed class CachedInkDocument
    {
        public DateTime LastWriteUtc { get; init; }
        public InkDocumentData Document { get; init; } = new();
    }

    /// <summary>
    /// Save ink strokes for a specific page of the given source file.
    /// Merges into the existing document data if present.
    /// </summary>
    public void SaveInkForFile(string sourceFilePath, int pageIndex, List<InkStrokeData> strokes)
    {
        if (string.IsNullOrWhiteSpace(sourceFilePath))
        {
            return;
        }

        if (!TryGetJsonPath(sourceFilePath, out var jsonPath))
        {
            return;
        }

        var doc = LoadDocumentWithCache(jsonPath) ?? new InkDocumentData
        {
            SourcePath = sourceFilePath
        };

        // Find or create page entry
        var page = doc.Pages.FirstOrDefault(p => p.PageIndex == pageIndex);
        if (page == null)
        {
            page = new InkPageData
            {
                PageIndex = pageIndex,
                SourcePath = sourceFilePath,
                DocumentName = Path.GetFileNameWithoutExtension(sourceFilePath)
            };
            doc.Pages.Add(page);
        }

        page.Strokes = strokes ?? new List<InkStrokeData>();
        page.UpdatedAt = DateTime.UtcNow;

        // Remove pages that have no strokes
        doc.Pages.RemoveAll(p => p.Strokes.Count == 0);

        if (doc.Pages.Count == 0)
        {
            // No strokes left — delete the sidecar file
            DeleteJsonFile(jsonPath);
            InvalidateCache(jsonPath);
            return;
        }

        if (!TryEnsureInkFolder(sourceFilePath, out _))
        {
            return;
        }

        var json = JsonSerializer.Serialize(doc, _options);
        WriteAllTextAtomically(jsonPath, json);
        RefreshCacheFromDisk(jsonPath, doc);
    }

    /// <summary>
    /// Save entire document data for the given source file.
    /// </summary>
    public void SaveDocument(string sourceFilePath, InkDocumentData doc)
    {
        if (string.IsNullOrWhiteSpace(sourceFilePath) || doc == null)
        {
            return;
        }

        doc.Pages.RemoveAll(p => p.Strokes.Count == 0);

        if (doc.Pages.Count == 0)
        {
            if (!TryGetJsonPath(sourceFilePath, out var jsonPath))
            {
                return;
            }

            DeleteJsonFile(jsonPath);
            InvalidateCache(jsonPath);
            return;
        }

        if (!TryEnsureInkFolder(sourceFilePath, out _)
            || !TryGetJsonPath(sourceFilePath, out var path))
        {
            return;
        }

        var json = JsonSerializer.Serialize(doc, _options);
        WriteAllTextAtomically(path, json);
        RefreshCacheFromDisk(path, doc);
    }

    /// <summary>
    /// Load all ink data for the given source file. Returns null if no sidecar exists.
    /// </summary>
    public InkDocumentData? LoadInkForFile(string sourceFilePath)
    {
        if (string.IsNullOrWhiteSpace(sourceFilePath))
        {
            return null;
        }
        if (!TryGetJsonPath(sourceFilePath, out var jsonPath))
        {
            return null;
        }

        return LoadDocumentWithCache(jsonPath);
    }

    /// <summary>
    /// Load ink strokes for a single page of the given source file.
    /// Returns null if no sidecar/page exists.
    /// </summary>
    public List<InkStrokeData>? LoadInkPageForFile(string sourceFilePath, int pageIndex)
    {
        if (string.IsNullOrWhiteSpace(sourceFilePath) || pageIndex <= 0)
        {
            return null;
        }

        var doc = LoadInkForFile(sourceFilePath);
        if (doc?.Pages == null || doc.Pages.Count == 0)
        {
            return null;
        }

        var page = doc.Pages.FirstOrDefault(p => p.PageIndex == pageIndex);
        if (page == null || page.Strokes == null || page.Strokes.Count == 0)
        {
            return null;
        }

        return page.Strokes;
    }

    /// <summary>
    /// Check whether ink data exists for the given source file.
    /// </summary>
    public bool HasInkForFile(string sourceFilePath)
    {
        if (string.IsNullOrWhiteSpace(sourceFilePath))
        {
            return false;
        }
        return TryGetJsonPath(sourceFilePath, out var jsonPath)
            && HasValidInkInSidecar(jsonPath);
    }

    /// <summary>
    /// Delete all ink data for the given source file.
    /// </summary>
    public void DeleteInkForFile(string sourceFilePath)
    {
        if (string.IsNullOrWhiteSpace(sourceFilePath))
        {
            return;
        }
        if (!TryGetJsonPath(sourceFilePath, out var jsonPath))
        {
            return;
        }

        DeleteJsonFile(jsonPath);
        InvalidateCache(jsonPath);
    }

    /// <summary>
    /// List all source files in the given directory that have sidecar ink data.
    /// </summary>
    public IReadOnlyList<string> ListFilesWithInk(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return Array.Empty<string>();
        }
        var inkFolder = Path.Combine(directoryPath, InkFolderName);
        if (!Directory.Exists(inkFolder))
        {
            return Array.Empty<string>();
        }
        var result = new List<string>();
        foreach (var jsonFile in EnumerateInkSidecarFilesSafe(inkFolder))
        {
            // filename is e.g. "lecture.pdf.ink.json" -> source file is "lecture.pdf"
            var jsonName = Path.GetFileName(jsonFile);
            var sourceName = GetSourceNameFromSidecarName(jsonName);
            if (!string.IsNullOrWhiteSpace(sourceName))
            {
                var sourcePath = Path.Combine(directoryPath, sourceName);
                if (File.Exists(sourcePath) && HasValidInkInSidecar(jsonFile))
                {
                    result.Add(sourcePath);
                }
            }
        }
        return result;
    }

    /// <summary>
    /// Remove orphan sidecar files whose source file no longer exists.
    /// Returns deleted sidecar count.
    /// </summary>
    public int CleanupOrphanSidecarsInDirectory(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return 0;
        }

        var inkFolder = Path.Combine(directoryPath, InkFolderName);
        if (!Directory.Exists(inkFolder))
        {
            return 0;
        }

        var deleted = 0;
        foreach (var jsonFile in EnumerateInkSidecarFilesSafe(inkFolder))
        {
            var jsonName = Path.GetFileName(jsonFile);
            var sourceName = GetSourceNameFromSidecarName(jsonName);
            if (string.IsNullOrWhiteSpace(sourceName))
            {
                continue;
            }

            var sourcePath = Path.Combine(directoryPath, sourceName);
            if (File.Exists(sourcePath))
            {
                continue;
            }

            if (!TryDeleteFileSafe(jsonFile))
            {
                // Ignore cleanup failures.
                continue;
            }

            InvalidateCache(jsonFile);
            deleted++;
        }

        return deleted;
    }

    // ── Internal helpers ──

    internal static string GetJsonPath(string sourceFilePath)
    {
        var directory = Path.GetDirectoryName(sourceFilePath) ?? string.Empty;
        var fileName = Path.GetFileName(sourceFilePath);
        return Path.Combine(directory, InkFolderName, $"{fileName}{SidecarSuffix}");
    }

    private static bool TryGetJsonPath(string sourceFilePath, out string jsonPath)
    {
        jsonPath = string.Empty;
        try
        {
            jsonPath = GetJsonPath(sourceFilePath);
            _ = Path.GetFullPath(jsonPath);
            return !string.IsNullOrWhiteSpace(jsonPath);
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine($"[InkPersistence] invalid source path path={sourceFilePath} ex={ex.GetType().Name} msg={ex.Message}");
            return false;
        }
    }

    private static bool TryEnsureInkFolder(string sourceFilePath, out string inkFolder)
    {
        inkFolder = string.Empty;
        try
        {
            var directory = Path.GetDirectoryName(sourceFilePath) ?? string.Empty;
            inkFolder = Path.Combine(directory, InkFolderName);
            if (!Directory.Exists(inkFolder))
            {
                var info = Directory.CreateDirectory(inkFolder);
                info.Attributes |= FileAttributes.Hidden;
            }

            return true;
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine($"[InkPersistence] ensure ink folder failed path={sourceFilePath} ex={ex.GetType().Name} msg={ex.Message}");
            return false;
        }
    }

    private InkDocumentData? LoadDocumentWithCache(string jsonPath)
    {
        if (!File.Exists(jsonPath))
        {
            InvalidateCache(jsonPath);
            return null;
        }

        var lastWriteUtc = GetLastWriteUtcSafe(jsonPath);
        lock (_cacheLock)
        {
            if (_documentCache.TryGetValue(jsonPath, out var cached)
                && cached.LastWriteUtc == lastWriteUtc)
            {
                return CloneDocument(cached.Document);
            }
        }

        var loaded = LoadDocumentFromDisk(jsonPath);
        if (loaded == null)
        {
            InvalidateCache(jsonPath);
            return null;
        }

        lock (_cacheLock)
        {
            _documentCache[jsonPath] = new CachedInkDocument
            {
                LastWriteUtc = lastWriteUtc,
                Document = CloneDocument(loaded)
            };
        }

        return loaded;
    }

    private InkDocumentData? LoadDocumentFromDisk(string jsonPath)
    {
        if (!File.Exists(jsonPath))
        {
            return null;
        }
        try
        {
            var json = File.ReadAllText(jsonPath);
            return JsonSerializer.Deserialize<InkDocumentData>(json, _options);
        }
        catch (JsonException)
        {
            Debug.WriteLine($"[InkPersistence] failed to parse sidecar json path={jsonPath}");
            return null;
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine($"[InkPersistence] failed to read sidecar json path={jsonPath} ex={ex.GetType().Name} msg={ex.Message}");
            return null;
        }
    }

    private bool HasValidInkInSidecar(string jsonPath)
    {
        if (!File.Exists(jsonPath))
        {
            return false;
        }

        var doc = LoadDocumentWithCache(jsonPath);
        if (doc?.Pages == null || doc.Pages.Count == 0)
        {
            return false;
        }

        foreach (var page in doc.Pages)
        {
            if (page.Strokes?.Count > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static void DeleteJsonFile(string jsonPath)
    {
        if (File.Exists(jsonPath))
        {
            _ = TryDeleteFileSafe(jsonPath);
        }
    }

    private static void WriteAllTextAtomically(string path, string content)
    {
        AtomicFileReplaceUtility.WriteAtomically(
            path,
            tempPath => File.WriteAllText(tempPath, content),
            onTempCleanupFailure: static (tempPath, ex) =>
            {
                if (!AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
                {
                    return;
                }

                Debug.WriteLine($"[InkPersistence] delete file failed path={tempPath} ex={ex.GetType().Name} msg={ex.Message}");
            });
    }

    private static DateTime GetLastWriteUtcSafe(string path)
    {
        try
        {
            return File.GetLastWriteTimeUtc(path);
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            return DateTime.MinValue;
        }
    }

    private static IEnumerable<string> EnumerateInkSidecarFilesSafe(string inkFolder)
    {
        try
        {
            return Directory.EnumerateFiles(inkFolder, "*.ink.json", TopLevelIgnoreInaccessibleOptions);
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            return Array.Empty<string>();
        }
    }

    private static string? GetSourceNameFromSidecarName(string? sidecarName)
    {
        if (string.IsNullOrWhiteSpace(sidecarName)
            || !sidecarName.EndsWith(SidecarSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var sourceName = sidecarName[..^SidecarSuffix.Length];
        return string.IsNullOrWhiteSpace(sourceName) ? null : sourceName;
    }

    private static bool TryDeleteFileSafe(string path)
    {
        try
        {
            File.Delete(path);
            return true;
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine($"[InkPersistence] delete file failed path={path} ex={ex.GetType().Name} msg={ex.Message}");
            return false;
        }
    }

    private void InvalidateCache(string jsonPath)
    {
        lock (_cacheLock)
        {
            _documentCache.Remove(jsonPath);
        }
    }

    private void RefreshCacheFromDisk(string jsonPath, InkDocumentData source)
    {
        var lastWriteUtc = GetLastWriteUtcSafe(jsonPath);
        lock (_cacheLock)
        {
            _documentCache[jsonPath] = new CachedInkDocument
            {
                LastWriteUtc = lastWriteUtc,
                Document = CloneDocument(source)
            };
        }
    }

    private static InkDocumentData CloneDocument(InkDocumentData source)
    {
        var clone = new InkDocumentData
        {
            Version = source.Version,
            SourcePath = source.SourcePath
        };

        foreach (var page in source.Pages)
        {
            clone.Pages.Add(new InkPageData
            {
                PageIndex = page.PageIndex,
                DocumentName = page.DocumentName,
                SourcePath = page.SourcePath,
                BackgroundImageFile = page.BackgroundImageFile,
                CreatedAt = page.CreatedAt,
                UpdatedAt = page.UpdatedAt,
                Strokes = CloneStrokes(page.Strokes)
            });
        }

        return clone;
    }

    private static List<InkStrokeData> CloneStrokes(List<InkStrokeData>? source)
    {
        if (source == null || source.Count == 0)
        {
            return new List<InkStrokeData>();
        }

        var clone = new List<InkStrokeData>(source.Count);
        foreach (var stroke in source)
        {
            clone.Add(new InkStrokeData
            {
                Type = stroke.Type,
                BrushStyle = stroke.BrushStyle,
                GeometryPath = stroke.GeometryPath,
                ColorHex = stroke.ColorHex,
                Opacity = stroke.Opacity,
                BrushSize = stroke.BrushSize,
                MaskSeed = stroke.MaskSeed,
                InkFlow = stroke.InkFlow,
                StrokeDirectionX = stroke.StrokeDirectionX,
                StrokeDirectionY = stroke.StrokeDirectionY,
                CalligraphyRenderMode = stroke.CalligraphyRenderMode,
                CalligraphyInkBloomEnabled = stroke.CalligraphyInkBloomEnabled,
                CalligraphySealEnabled = stroke.CalligraphySealEnabled,
                CalligraphyOverlayOpacityThreshold = stroke.CalligraphyOverlayOpacityThreshold,
                ReferenceWidth = stroke.ReferenceWidth,
                ReferenceHeight = stroke.ReferenceHeight,
                Ribbons = stroke.Ribbons
                    .Select(r => new InkRibbonData
                    {
                        GeometryPath = r.GeometryPath,
                        Opacity = r.Opacity,
                        RibbonT = r.RibbonT
                    })
                    .ToList(),
                Blooms = stroke.Blooms
                    .Select(b => new InkBloomData
                    {
                        GeometryPath = b.GeometryPath,
                        Opacity = b.Opacity
                    })
                    .ToList()
            });
        }

        return clone;
    }
}
