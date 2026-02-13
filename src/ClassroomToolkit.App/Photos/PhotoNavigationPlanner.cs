using System;
using System.Collections.Generic;

namespace ClassroomToolkit.App.Photos;

public enum PhotoFileType
{
    Unknown = 0,
    Image = 1,
    Pdf = 2
}

public readonly record struct PhotoNavigationRequest(
    IReadOnlyList<string> Sequence,
    int CurrentIndex,
    string? CurrentPath,
    int Direction,
    PhotoFileType CurrentFileTypeHint = PhotoFileType.Unknown);

public readonly record struct PhotoNavigationDecision(
    bool ShouldNavigateFile,
    int ResolvedCurrentIndex,
    int NextIndex,
    string CurrentPath,
    PhotoFileType CurrentFileType);

public static class PhotoNavigationPlanner
{
    public static PhotoNavigationDecision Plan(PhotoNavigationRequest request)
    {
        if (request.Direction == 0 || request.Sequence.Count == 0)
        {
            return new PhotoNavigationDecision(
                ShouldNavigateFile: false,
                ResolvedCurrentIndex: -1,
                NextIndex: -1,
                CurrentPath: string.Empty,
                CurrentFileType: PhotoFileType.Unknown);
        }

        var resolvedCurrentIndex = ResolveCurrentIndex(request.Sequence, request.CurrentIndex, request.CurrentPath);
        if (resolvedCurrentIndex < 0 || resolvedCurrentIndex >= request.Sequence.Count)
        {
            return new PhotoNavigationDecision(
                ShouldNavigateFile: false,
                ResolvedCurrentIndex: -1,
                NextIndex: -1,
                CurrentPath: string.Empty,
                CurrentFileType: PhotoFileType.Unknown);
        }

        var currentPath = request.Sequence[resolvedCurrentIndex];
        var currentType = request.CurrentFileTypeHint == PhotoFileType.Unknown
            ? ClassifyPath(currentPath)
            : request.CurrentFileTypeHint;

        // File-level navigation policy:
        // - PDF cannot switch to any other file.
        // - Image can switch only to another image file.
        if (currentType != PhotoFileType.Image)
        {
            return new PhotoNavigationDecision(
                ShouldNavigateFile: false,
                ResolvedCurrentIndex: resolvedCurrentIndex,
                NextIndex: -1,
                CurrentPath: currentPath,
                CurrentFileType: currentType);
        }

        var nextIndex = FindNextImageIndex(request.Sequence, resolvedCurrentIndex, request.Direction);
        if (nextIndex < 0)
        {
            return new PhotoNavigationDecision(
                ShouldNavigateFile: false,
                ResolvedCurrentIndex: resolvedCurrentIndex,
                NextIndex: -1,
                CurrentPath: currentPath,
                CurrentFileType: currentType);
        }

        return new PhotoNavigationDecision(
            ShouldNavigateFile: true,
            ResolvedCurrentIndex: resolvedCurrentIndex,
            NextIndex: nextIndex,
            CurrentPath: currentPath,
            CurrentFileType: currentType);
    }

    private static int ResolveCurrentIndex(IReadOnlyList<string> sequence, int currentIndex, string? currentPath)
    {
        if (!string.IsNullOrWhiteSpace(currentPath))
        {
            for (int i = 0; i < sequence.Count; i++)
            {
                if (string.Equals(sequence[i], currentPath, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
        }

        if (currentIndex >= 0 && currentIndex < sequence.Count)
        {
            return currentIndex;
        }

        return -1;
    }

    private static int FindNextImageIndex(IReadOnlyList<string> sequence, int startIndex, int direction)
    {
        var index = startIndex + direction;
        while (index >= 0 && index < sequence.Count)
        {
            if (ClassifyPath(sequence[index]) == PhotoFileType.Image)
            {
                return index;
            }
            index += direction;
        }

        return -1;
    }

    public static PhotoFileType ClassifyPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return PhotoFileType.Unknown;
        }

        if (path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return PhotoFileType.Pdf;
        }

        if (path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".gif", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".tif", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".tiff", StringComparison.OrdinalIgnoreCase))
        {
            return PhotoFileType.Image;
        }

        return PhotoFileType.Unknown;
    }
}
