using System;

namespace ClassroomToolkit.App.Photos;

internal readonly record struct ImageManagerInkCleanupSummary(
    int SidecarsDeleted,
    int CompositesDeleted);

internal static class ImageManagerInkCleanupExecutor
{
    internal static ImageManagerInkCleanupSummary Cleanup(
        string folder,
        Func<string, int> cleanupSidecars,
        Func<string, int> cleanupComposites)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return new ImageManagerInkCleanupSummary(0, 0);
        }

        return new ImageManagerInkCleanupSummary(
            SidecarsDeleted: cleanupSidecars(folder),
            CompositesDeleted: cleanupComposites(folder));
    }
}
