using System;
using System.Windows.Media;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Paint.Brushes;
using MediaPen = System.Windows.Media.Pen;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private bool ShouldRecordRuntimeInkStroke()
    {
        // Cross-page photo/PDF writing requires per-page vector strokes to survive page switches.
        // Keep runtime stroke recording in photo ink mode even when replay/history recording is disabled.
        return _inkRecordEnabled || IsPhotoInkModeActive();
    }

    private void RecordBrushStroke(Geometry geometry)
    {
        if (!ShouldRecordRuntimeInkStroke() || geometry == null)
        {
            return;
        }
        var stroke = new InkStrokeData
        {
            Type = InkStrokeType.Brush,
            BrushStyle = _brushStyle,
            ColorHex = ToHex(EffectiveBrushColor()),
            Opacity = _brushOpacity,
            BrushSize = _brushSize,
            CalligraphyRenderMode = _calligraphyRenderMode,
            CalligraphyInkBloomEnabled = _calligraphyInkBloomEnabled,
            CalligraphySealEnabled = _calligraphySealEnabled,
            CalligraphyOverlayOpacityThreshold = _calligraphyOverlayOpacityThreshold
        };
        if (TryGetCurrentPhotoReferenceSize(out var refWidth, out var refHeight))
        {
            stroke.ReferenceWidth = refWidth;
            stroke.ReferenceHeight = refHeight;
        }
        var photoInkModeActive = IsPhotoInkModeActive();
        if (_brushStyle == PaintBrushStyle.Calligraphy && _activeRenderer is VariableWidthBrushRenderer calligraphyRenderer)
        {
            var core = calligraphyRenderer.GetLastCoreGeometry();
            var strokeGeometry = core ?? geometry;
            if (!CalligraphySinglePassCompositeEnabled)
            {
                var ribbons = calligraphyRenderer.GetLastRibbonGeometries();
                if (ribbons != null && ribbons.Count > 0)
                {
                    foreach (var ribbon in ribbons)
                    {
                        var ribbonGeometry = photoInkModeActive ? ToPhotoGeometry(ribbon.Geometry) : ribbon.Geometry;
                        if (ribbonGeometry == null)
                        {
                            continue;
                        }
                        stroke.Ribbons.Add(new InkRibbonData
                        {
                            GeometryPath = InkGeometrySerializer.Serialize(ribbonGeometry),
                            Opacity = calligraphyRenderer.GetRibbonOpacity(ribbon.RibbonT),
                            RibbonT = ribbon.RibbonT
                        });
                    }
                }
            }
            var storeGeometry = photoInkModeActive ? ToPhotoGeometry(strokeGeometry) : strokeGeometry;
            if (storeGeometry == null)
            {
                return;
            }
            stroke.GeometryPath = InkGeometrySerializer.Serialize(storeGeometry);
            stroke.InkFlow = calligraphyRenderer.LastInkFlow;
            stroke.StrokeDirectionX = calligraphyRenderer.LastStrokeDirection.X;
            stroke.StrokeDirectionY = calligraphyRenderer.LastStrokeDirection.Y;
            if (!CalligraphySinglePassCompositeEnabled)
            {
                var blooms = calligraphyRenderer.GetInkBloomGeometries();
                if (blooms != null)
                {
                    foreach (var bloom in blooms)
                    {
                        var bloomGeometry = photoInkModeActive ? ToPhotoGeometry(bloom.Geometry) : bloom.Geometry;
                        if (bloomGeometry == null)
                        {
                            continue;
                        }
                        stroke.Blooms.Add(new InkBloomData
                        {
                            GeometryPath = InkGeometrySerializer.Serialize(bloomGeometry),
                            Opacity = bloom.Opacity
                        });
                    }
                }
            }
        }
        else
        {
            var storeGeometry = photoInkModeActive ? ToPhotoGeometry(geometry) : geometry;
            if (storeGeometry == null)
            {
                return;
            }
            stroke.GeometryPath = InkGeometrySerializer.Serialize(storeGeometry);
        }
        if (string.IsNullOrWhiteSpace(stroke.GeometryPath))
        {
            return;
        }
        stroke.MaskSeed = ComputeDeterministicMaskSeed(stroke);
        CommitStroke(stroke);
    }

    private void RecordShapeStroke(Geometry geometry, MediaPen pen)
    {
        if (!ShouldRecordRuntimeInkStroke() || geometry == null || pen == null)
        {
            return;
        }
        var widened = geometry.GetWidenedPathGeometry(pen);
        if (widened == null || widened.Bounds.IsEmpty)
        {
            return;
        }
        var storeGeometry = IsPhotoInkModeActive() ? ToPhotoGeometry(widened) : widened;
        if (storeGeometry == null)
        {
            return;
        }
        var stroke = new InkStrokeData
        {
            Type = InkStrokeType.Shape,
            BrushStyle = PaintBrushStyle.StandardRibbon,
            ColorHex = ToHex(EffectiveBrushColor()),
            Opacity = _brushOpacity,
            BrushSize = _brushSize,
            CalligraphyRenderMode = CalligraphyRenderMode.Clarity,
            GeometryPath = InkGeometrySerializer.Serialize(storeGeometry)
        };
        if (TryGetCurrentPhotoReferenceSize(out var refWidth, out var refHeight))
        {
            stroke.ReferenceWidth = refWidth;
            stroke.ReferenceHeight = refHeight;
        }
        if (string.IsNullOrWhiteSpace(stroke.GeometryPath))
        {
            return;
        }
        stroke.MaskSeed = ComputeDeterministicMaskSeed(stroke);
        CommitStroke(stroke);
    }

    private static int ComputeDeterministicMaskSeed(InkStrokeData stroke)
    {
        uint hash = 2166136261u;
        hash = Fnv1a(hash, stroke.Type);
        hash = Fnv1a(hash, stroke.BrushStyle);
        hash = Fnv1a(hash, stroke.CalligraphyRenderMode);
        hash = Fnv1a(hash, stroke.ColorHex);
        hash = Fnv1a(hash, Quantize(stroke.BrushSize, 1000.0));
        hash = Fnv1a(hash, stroke.GeometryPath);
        int seed = unchecked((int)hash);
        return seed == 0 ? 17 : seed;
    }

    private static uint Fnv1a(uint hash, PaintBrushStyle value)
    {
        return Fnv1a(hash, (int)value);
    }

    private static uint Fnv1a(uint hash, InkStrokeType value)
    {
        return Fnv1a(hash, (int)value);
    }

    private static uint Fnv1a(uint hash, CalligraphyRenderMode value)
    {
        return Fnv1a(hash, (int)value);
    }

    private static uint Fnv1a(uint hash, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return Fnv1a(hash, 0);
        }

        unchecked
        {
            for (int i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= 16777619u;
            }
        }
        return hash;
    }

    private static uint Fnv1a(uint hash, int value)
    {
        unchecked
        {
            hash ^= (uint)value;
            hash *= 16777619u;
        }
        return hash;
    }

    private static int Quantize(double value, double scale)
    {
        if (!double.IsFinite(value))
        {
            return 0;
        }

        return (int)Math.Round(value * scale);
    }
}

