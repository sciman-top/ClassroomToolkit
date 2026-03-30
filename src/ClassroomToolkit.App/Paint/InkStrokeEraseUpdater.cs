using System;
using ClassroomToolkit.App.Ink;

namespace ClassroomToolkit.App.Paint;

internal static class InkStrokeEraseUpdater
{
    internal static bool TryApplyUpdatedGeometryPath(InkStrokeData stroke, string? updatedPath, out bool removed)
    {
        removed = false;
        if (updatedPath == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(updatedPath))
        {
            removed = true;
            return true;
        }

        if (string.Equals(updatedPath, stroke.GeometryPath, StringComparison.Ordinal))
        {
            return false;
        }

        stroke.GeometryPath = updatedPath;
        stroke.CachedGeometry = null;
        stroke.CachedBounds = null;
        stroke.CachedRibbonGeometries = null;
        return true;
    }
}
