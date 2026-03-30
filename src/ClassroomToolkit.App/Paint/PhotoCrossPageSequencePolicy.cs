using System;
using System.Collections.Generic;
using System.Linq;
using ClassroomToolkit.Application.UseCases.Photos;

namespace ClassroomToolkit.App.Paint;

internal static class PhotoCrossPageSequencePolicy
{
    internal static (IReadOnlyList<string> Sequence, int CurrentIndex) Normalize(
        IReadOnlyList<string>? sequence,
        int currentIndex)
    {
        var source = sequence?.ToList() ?? new List<string>();
        if (source.Count == 0)
        {
            return (Array.Empty<string>(), -1);
        }

        string? currentPath = currentIndex >= 0 && currentIndex < source.Count
            ? source[currentIndex]
            : null;

        var imageOnly = source
            .Where(path => PhotoNavigationPlanner.ClassifyPath(path) == PhotoFileType.Image)
            .ToList();
        if (imageOnly.Count == 0)
        {
            return (Array.Empty<string>(), -1);
        }

        var normalizedIndex = -1;
        if (!string.IsNullOrWhiteSpace(currentPath))
        {
            normalizedIndex = imageOnly.FindIndex(path =>
                string.Equals(path, currentPath, StringComparison.OrdinalIgnoreCase));
        }

        if (normalizedIndex < 0)
        {
            normalizedIndex = Math.Clamp(currentIndex, 0, imageOnly.Count - 1);
        }

        return (imageOnly, normalizedIndex);
    }
}
