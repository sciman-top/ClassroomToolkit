using System;
using System.Collections.Generic;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Shapes;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Paint.Brushes;
using ClassroomToolkit.App.Session;
using ClassroomToolkit.App.Windowing;
using MediaColor = System.Windows.Media.Color;
using WpfPath = System.Windows.Shapes.Path;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaBrush = System.Windows.Media.Brush;
using MediaPen = System.Windows.Media.Pen;
using WpfRectangle = System.Windows.Shapes.Rectangle;
using WpfPoint = System.Windows.Point;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private bool ApplyInkErase(Geometry geometry)
    {
        if (_inkStrokes.Count == 0 || geometry == null)
        {
            return false;
        }
        var photoInkModeActive = IsPhotoInkModeActive();
        var erasePrimary = photoInkModeActive ? ToPhotoGeometry(geometry) : geometry;
        var eraseFallback = photoInkModeActive ? geometry : null;
        if (erasePrimary == null)
        {
            return false;
        }
        bool changed = false;
        for (int i = _inkStrokes.Count - 1; i >= 0; i--)
        {
            var stroke = _inkStrokes[i];
            var geometryPathChanged = false;
            var bloomGeometryChanged = false;
            var ribbonGeometryChanged = false;
            var updatedPath = ExcludeGeometryWithFallback(stroke.GeometryPath, erasePrimary, eraseFallback);
            if (!InkStrokeEraseUpdater.TryApplyUpdatedGeometryPath(stroke, updatedPath, out var strokeRemoved))
            {
                strokeRemoved = false;
            }
            else if (strokeRemoved)
            {
                _inkStrokes.RemoveAt(i);
                changed = true;
                continue;
            }
            else
            {
                geometryPathChanged = true;
            }

            if (stroke.Blooms.Count > 0)
            {
                for (int j = stroke.Blooms.Count - 1; j >= 0; j--)
                {
                    var bloom = stroke.Blooms[j];
                    var bloomUpdated = ExcludeGeometryWithFallback(bloom.GeometryPath, erasePrimary, eraseFallback);
                    if (string.IsNullOrWhiteSpace(bloomUpdated))
                    {
                        stroke.Blooms.RemoveAt(j);
                        bloomGeometryChanged = true;
                        changed = true;
                        continue;
                    }
                    if (!string.Equals(bloomUpdated, bloom.GeometryPath, StringComparison.Ordinal))
                    {
                        bloom.GeometryPath = bloomUpdated;
                        bloomGeometryChanged = true;
                    }
                }
            }
            if (stroke.Ribbons.Count > 0)
            {
                bool ribbonsChanged = false;
                for (int j = stroke.Ribbons.Count - 1; j >= 0; j--)
                {
                    var ribbon = stroke.Ribbons[j];
                    var ribbonUpdated = ExcludeGeometryWithFallback(ribbon.GeometryPath, erasePrimary, eraseFallback);
                    if (string.IsNullOrWhiteSpace(ribbonUpdated))
                    {
                        stroke.Ribbons.RemoveAt(j);
                        ribbonsChanged = true;
                        ribbonGeometryChanged = true;
                        changed = true;
                        continue;
                    }
                    if (!string.Equals(ribbonUpdated, ribbon.GeometryPath, StringComparison.Ordinal))
                    {
                        ribbon.GeometryPath = ribbonUpdated;
                        ribbonsChanged = true;
                        ribbonGeometryChanged = true;
                        changed = true;
                    }
                }
                if (ribbonsChanged)
                {
                    stroke.CachedRibbonGeometries = null;
                }
            }
            if (InkEraseStrokeChangePolicy.ShouldMarkStrokeChanged(
                    geometryPathChanged,
                    bloomGeometryChanged,
                    ribbonGeometryChanged))
            {
                changed = true;
            }
        }
        return changed;
    }

    private static string? ExcludeGeometryWithFallback(string geometryPath, Geometry primaryEraser, Geometry? fallbackEraser)
    {
        var primary = ExcludeGeometry(geometryPath, primaryEraser);
        if (fallbackEraser == null)
        {
            return primary;
        }
        if (primary == null || string.Equals(primary, geometryPath, StringComparison.Ordinal))
        {
            return ExcludeGeometry(geometryPath, fallbackEraser);
        }
        return primary;
    }

}
