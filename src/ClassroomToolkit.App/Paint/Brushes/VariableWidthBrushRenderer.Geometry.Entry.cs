using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace ClassroomToolkit.App.Paint.Brushes;

public partial class VariableWidthBrushRenderer
{
    public Geometry? GetLastStrokeGeometry()
    {
        if (_points.Count < 2) return null;
        var geometry = GenerateGeometry();
        if (geometry != null)
        {
            geometry.Freeze();
        }
        return geometry;
    }

    public IReadOnlyList<RibbonGeometry>? GetLastRibbonGeometries()
    {
        EnsureGeometryCache();
        return _cachedRibbons == null || _cachedRibbons.Count == 0 ? null : _cachedRibbons;
    }

    public Geometry? GetLastCoreGeometry()
    {
        EnsureGeometryCache();
        return _cachedCoreGeometry;
    }

    public Geometry? GetPreviewCoreGeometry()
    {
        if (!_cacheDirty && _cachedPreviewGeometry != null)
        {
            return _cachedPreviewGeometry;
        }

        if (_points.Count < 2)
        {
            _cachedPreviewGeometry = null;
            return null;
        }

        Geometry? preview;
        if (_points.Count <= PreviewTailPointWindow + 4)
        {
            _previewBaseGeometry = null;
            _previewBasePointCount = 0;
            var samples = BuildCenterlineSamplesFinal(_points, previewFastPath: true);
            preview = samples.Count < 2 ? null : BuildRibbonGeometry(samples, ribbonT: 0, noiseSeedOffset: 0);
        }
        else
        {
            int basePointCount = Math.Max(2, _points.Count - PreviewTailPointWindow);
            bool shouldRefreshBase = _previewBaseGeometry == null
                || _previewBasePointCount <= 0
                || basePointCount < _previewBasePointCount
                || (basePointCount - _previewBasePointCount) >= PreviewBaseRefreshStride;

            if (shouldRefreshBase)
            {
                _previewBasePointCount = basePointCount;
                _previewBaseGeometry = BuildPreviewGeometryForRange(0, _previewBasePointCount);
                if (_previewBaseGeometry?.CanFreeze == true)
                {
                    _previewBaseGeometry.Freeze();
                }
            }

            int tailStart = Math.Max(0, _previewBasePointCount - 3);
            var tailGeometry = BuildPreviewGeometryForRange(tailStart, _points.Count);
            if (_previewBaseGeometry != null && tailGeometry != null)
            {
                var group = new GeometryGroup { FillRule = FillRule.Nonzero };
                group.Children.Add(_previewBaseGeometry);
                group.Children.Add(tailGeometry);
                preview = group;
            }
            else
            {
                preview = tailGeometry ?? _previewBaseGeometry;
            }
        }

        if (preview?.CanFreeze == true)
        {
            preview.Freeze();
        }
        _cachedPreviewGeometry = preview;
        return _cachedPreviewGeometry;
    }

    private Geometry? BuildPreviewGeometryForRange(int startInclusive, int endExclusive)
    {
        int start = Math.Max(0, startInclusive);
        int end = Math.Min(_points.Count, endExclusive);
        int count = end - start;
        if (count < 2)
        {
            return null;
        }

        var source = CopyRangeToPreviewSliceBuffer(start, end);
        var samples = BuildCenterlineSamplesFinal(source, previewFastPath: true);
        if (samples.Count < 2)
        {
            return null;
        }

        return BuildRibbonGeometry(samples, ribbonT: 0, noiseSeedOffset: 0);
    }

    private IReadOnlyList<StrokePoint> CopyRangeToPreviewSliceBuffer(int startInclusive, int endExclusive)
    {
        _previewSliceBuffer.Clear();
        int start = Math.Max(0, startInclusive);
        int end = Math.Min(_points.Count, endExclusive);
        for (int i = start; i < end; i++)
        {
            _previewSliceBuffer.Add(_points[i]);
        }
        return _previewSliceBuffer;
    }

    public IReadOnlyList<InkBloomGeometry>? GetInkBloomGeometries()
    {
        EnsureGeometryCache();
        if (_cachedBlooms == null)
        {
            _cachedBlooms = BuildInkBloomGeometries();
        }
        return _cachedBlooms.Count == 0 ? null : _cachedBlooms;
    }

    public double GetRibbonOpacity(double ribbonT)
    {
        double baseOpacity = Lerp(1.0, 0.38, ribbonT);
        double inkOpacity = Lerp(0.55, 1.0, _lastInkFlow);
        return Math.Clamp(baseOpacity * inkOpacity, 0.18, 1.0);
    }

    private Geometry? GenerateGeometry()
    {
        if (_points.Count < 2) return null;
        var samples = BuildCenterlineSamplesFinal();
        if (samples.Count < 2) return null;

        var geometries = BuildRibbonGeometries(samples);
        if (geometries.Count == 0) return null;
        if (geometries.Count == 1) return geometries[0].Geometry;

        var group = new GeometryGroup
        {
            FillRule = FillRule.Nonzero
        };
        foreach (var item in geometries)
        {
            group.Children.Add(item.Geometry);
        }
        return group;
    }

    private List<RibbonGeometry> BuildRibbonGeometries(List<StrokePoint> samples)
    {
        var result = new List<RibbonGeometry>();
        int ribbonCount = ResolveRibbonCount();
        if (ribbonCount <= 1)
        {
            var single = BuildRibbonGeometry(samples, 0, 0);
            if (single != null) result.Add(new RibbonGeometry(single, 0));
            return result;
        }

        double centerIndex = (ribbonCount - 1) * 0.5;
        for (int i = 0; i < ribbonCount; i++)
        {
            double ribbonT = centerIndex > 0 ? Math.Abs(i - centerIndex) / centerIndex : 0;
            var ribbonSamples = BuildRibbonSamples(samples, i, ribbonCount);
            var ribbonGeometry = BuildRibbonGeometry(ribbonSamples, ribbonT, i * 17.7);
            if (ribbonGeometry != null)
            {
                result.Add(new RibbonGeometry(ribbonGeometry, ribbonT));
            }
        }

        return result;
    }
}
