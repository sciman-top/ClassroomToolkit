using System;
using System.Collections.Generic;
using System.IO;

namespace ClassroomToolkit.App.Paint;

internal sealed class InkDirtyPageCoordinator
{
    private readonly Dictionary<string, InkPageRuntimeState> _pageStates = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _sessionModifiedPages = new(StringComparer.OrdinalIgnoreCase);

    internal void MarkLoaded(string sourcePath, int pageIndex, string hash)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || pageIndex <= 0)
        {
            return;
        }

        var state = GetOrCreate(sourcePath, pageIndex);
        state.Loaded = true;
        state.Dirty = false;
        state.LastKnownHash = hash;
        state.LastSavedHash = hash;
    }

    internal void MarkModified(string sourcePath, int pageIndex, string hash)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || pageIndex <= 0)
        {
            return;
        }

        var key = BuildRuntimePageStateKey(sourcePath, pageIndex);
        var state = GetOrCreate(sourcePath, pageIndex);
        state.Loaded = true;
        state.Dirty = true;
        state.Version++;
        state.LastKnownHash = hash;
        _sessionModifiedPages.Add(key);
    }

    internal void MarkPersisted(string sourcePath, int pageIndex, string hash)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || pageIndex <= 0)
        {
            return;
        }

        var state = GetOrCreate(sourcePath, pageIndex);
        state.Loaded = true;
        state.Dirty = false;
        state.LastKnownHash = hash;
        state.LastSavedHash = hash;
    }

    internal bool MarkPersistedIfUnchanged(string sourcePath, int pageIndex, string hash)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || pageIndex <= 0)
        {
            return false;
        }

        var state = GetOrCreate(sourcePath, pageIndex);
        if (!string.IsNullOrWhiteSpace(state.LastKnownHash)
            && !string.Equals(state.LastKnownHash, hash, StringComparison.Ordinal))
        {
            return false;
        }

        state.Loaded = true;
        state.Dirty = false;
        state.LastKnownHash = hash;
        state.LastSavedHash = hash;
        return true;
    }

    internal bool IsDirty(string sourcePath, int pageIndex)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || pageIndex <= 0)
        {
            return false;
        }

        var key = BuildRuntimePageStateKey(sourcePath, pageIndex);
        return _pageStates.TryGetValue(key, out var state) && state.Dirty;
    }

    internal bool WasModifiedInSession(string sourcePath, int pageIndex)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || pageIndex <= 0)
        {
            return false;
        }

        return _sessionModifiedPages.Contains(BuildRuntimePageStateKey(sourcePath, pageIndex));
    }

    internal IEnumerable<string> EnumerateSessionModifiedSourcesInDirectory(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            yield break;
        }

        foreach (var runtimeKey in _sessionModifiedPages)
        {
            if (!TryParseRuntimePageStateKey(runtimeKey, out var sourcePath, out _))
            {
                continue;
            }
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                continue;
            }
            if (!string.Equals(Path.GetDirectoryName(sourcePath), directoryPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            yield return sourcePath;
        }
    }

    internal IReadOnlyList<(string SourcePath, int PageIndex)> GetDirtyPages(string? directoryPath)
    {
        var result = new List<(string SourcePath, int PageIndex)>();
        foreach (var entry in _pageStates)
        {
            if (!entry.Value.Dirty)
            {
                continue;
            }

            if (!TryParseRuntimePageStateKey(entry.Key, out var sourcePath, out var pageIndex))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(directoryPath) &&
                !string.Equals(Path.GetDirectoryName(sourcePath), directoryPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            result.Add((sourcePath, pageIndex));
        }

        return result;
    }

    internal bool TryGetRuntimeState(string sourcePath, int pageIndex, out int version, out string lastKnownHash, out bool dirty)
    {
        version = 0;
        lastKnownHash = string.Empty;
        dirty = false;
        if (string.IsNullOrWhiteSpace(sourcePath) || pageIndex <= 0)
        {
            return false;
        }

        var key = BuildRuntimePageStateKey(sourcePath, pageIndex);
        if (!_pageStates.TryGetValue(key, out var state))
        {
            return false;
        }

        version = state.Version;
        lastKnownHash = state.LastKnownHash;
        dirty = state.Dirty;
        return true;
    }

    private InkPageRuntimeState GetOrCreate(string sourcePath, int pageIndex)
    {
        var key = BuildRuntimePageStateKey(sourcePath, pageIndex);
        if (!_pageStates.TryGetValue(key, out var state))
        {
            state = new InkPageRuntimeState();
            _pageStates[key] = state;
        }

        return state;
    }

    internal static string BuildRuntimePageStateKey(string sourcePath, int pageIndex)
    {
        var normalizedSourcePath = NormalizeSourcePath(sourcePath);
        return $"src|{normalizedSourcePath}|page|{pageIndex}";
    }

    internal static bool TryParseRuntimePageStateKey(string runtimeKey, out string sourcePath, out int pageIndex)
    {
        sourcePath = string.Empty;
        pageIndex = 0;
        if (string.IsNullOrWhiteSpace(runtimeKey) || !runtimeKey.StartsWith("src|", StringComparison.Ordinal))
        {
            return false;
        }

        const string marker = "|page|";
        var markerIndex = runtimeKey.LastIndexOf(marker, StringComparison.Ordinal);
        if (markerIndex <= "src|".Length)
        {
            return false;
        }

        sourcePath = runtimeKey["src|".Length..markerIndex];
        var pageRaw = runtimeKey[(markerIndex + marker.Length)..];
        if (!int.TryParse(pageRaw, out pageIndex) || pageIndex <= 0)
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(sourcePath);
    }

    private static string NormalizeSourcePath(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetFullPath(sourcePath);
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            return sourcePath;
        }
    }

    private sealed class InkPageRuntimeState
    {
        public bool Loaded { get; set; }
        public bool Dirty { get; set; }
        public int Version { get; set; }
        public string LastKnownHash { get; set; } = string.Empty;
        public string LastSavedHash { get; set; } = string.Empty;
    }
}
