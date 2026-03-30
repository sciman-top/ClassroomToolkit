using System;
using System.Collections.Generic;

namespace ClassroomToolkit.App.Ink;

internal static class InkCleanupCandidateDirectoryPolicy
{
    internal static IReadOnlyCollection<string> Resolve(
        string? baseDirectory,
        string? inkPhotoRootPath,
        IEnumerable<string>? recentFolders,
        IEnumerable<string>? favoriteFolders,
        Func<string, bool> directoryExists)
    {
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddIfValid(baseDirectory);
        AddIfValid(inkPhotoRootPath);
        AddRangeIfValid(recentFolders);
        AddRangeIfValid(favoriteFolders);
        return candidates;

        void AddRangeIfValid(IEnumerable<string>? folders)
        {
            if (folders == null)
            {
                return;
            }

            foreach (var folder in folders)
            {
                AddIfValid(folder);
            }
        }

        void AddIfValid(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            if (directoryExists(path))
            {
                candidates.Add(path);
            }
        }
    }
}
