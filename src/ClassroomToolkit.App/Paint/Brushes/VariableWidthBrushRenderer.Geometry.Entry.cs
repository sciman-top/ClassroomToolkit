using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using WpfPoint = System.Windows.Point;

namespace ClassroomToolkit.App.Paint.Brushes;

internal partial class VariableWidthBrushRenderer
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
            _previewBaseGlobalTotalLength = 0.0;
            var samples = BuildCenterlineSamplesFinal(_points, previewFastPath: true);
            preview = BuildPreviewCompositeGeometry(samples, includeStartCap: true, includeEndCap: true);
        }
        else
        {
            int basePointCount = Math.Max(2, _points.Count - PreviewTailPointWindow);
            double globalTotalLength = ComputePolylineLength(_points);
            double totalLengthDelta = Math.Abs(globalTotalLength - _previewBaseGlobalTotalLength);
            double refreshLengthThreshold = Math.Max(2.0, _baseSize * 0.4);
            bool shouldRefreshBase = _previewBaseGeometry == null
                || _previewBasePointCount <= 0
                || basePointCount < _previewBasePointCount
                || (basePointCount - _previewBasePointCount) >= PreviewBaseRefreshStride
                || totalLengthDelta >= refreshLengthThreshold;

            if (shouldRefreshBase)
            {
                _previewBasePointCount = basePointCount;
                _previewBaseGlobalTotalLength = globalTotalLength;
                _previewBaseGeometry = BuildPreviewGeometryForRange(
                    0,
                    _previewBasePointCount,
                    includeStartCap: true,
                    includeEndCap: false);
                if (_previewBaseGeometry?.CanFreeze == true)
                {
                    _previewBaseGeometry.Freeze();
                }
            }

            int tailStart = Math.Max(0, _previewBasePointCount - 3);
            var tailGeometry = BuildPreviewGeometryForRange(
                tailStart,
                _points.Count,
                includeStartCap: false,
                includeEndCap: true);
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

    internal Geometry? BuildPredictionGeometry(
        WpfPoint p0,
        WpfPoint p1,
        WpfPoint p2,
        double w0,
        double w1,
        double w2)
    {
        var samples = new List<StrokePoint>(3)
        {
            new(p0, ClampWidth(w0), progress: 0.0, wetness: _inkWetness),
            new(p1, ClampWidth(w1), progress: 0.5, wetness: _inkWetness),
            new(p2, ClampWidth(w2), progress: 1.0, wetness: _inkWetness)
        };
        var ribbons = BuildRibbonGeometries(samples, includeStartCap: false, includeEndCap: true);
        if (ribbons.Count == 0)
        {
            return null;
        }
        if (ribbons.Count == 1)
        {
            return ribbons[0].Geometry;
        }

        var group = new GeometryGroup { FillRule = FillRule.Nonzero };
        foreach (var ribbon in ribbons)
        {
            group.Children.Add(ribbon.Geometry);
        }
        if (group.CanFreeze)
        {
            group.Freeze();
        }
        return group;
    }

    private Geometry? BuildPreviewGeometryForRange(
        int startInclusive,
        int endExclusive,
        bool includeStartCap,
        bool includeEndCap)
    {
        int start = Math.Max(0, startInclusive);
        int end = Math.Min(_points.Count, endExclusive);
        int count = end - start;
        if (count < 2)
        {
            return null;
        }

        var source = CopyRangeToPreviewSliceBuffer(start, end);
        double globalStartLength = ComputePolylineLength(_points, start);
        double globalTotalLength = ComputePolylineLength(_points);
        var samples = BuildCenterlineSamplesFinal(
            source,
            previewFastPath: true,
            globalStartLength,
            globalTotalLength);
        if (samples.Count < 2)
        {
            return null;
        }

        return BuildPreviewCompositeGeometry(samples, includeStartCap, includeEndCap);
    }

    /// <summary>
    /// 预览几何与最终几何使用相同的多毫结构，避免抬笔时宽度跳变；
    /// 仅采样密度走快速路径。
    /// </summary>
    private Geometry? BuildPreviewCompositeGeometry(
        List<StrokePoint> samples,
        bool includeStartCap,
        bool includeEndCap)
    {
        if (samples.Count < 2)
        {
            return null;
        }

        var ribbons = BuildRibbonGeometries(samples, includeStartCap, includeEndCap);
        if (ribbons.Count == 0)
        {
            return null;
        }
        if (ribbons.Count == 1)
        {
            return ribbons[0].Geometry;
        }

        var group = new GeometryGroup { FillRule = FillRule.Nonzero };
        foreach (var ribbon in ribbons)
        {
            group.Children.Add(ribbon.Geometry);
        }
        return group;
    }

    private List<StrokePoint> CopyRangeToPreviewSliceBuffer(int startInclusive, int endExclusive)
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

    private Geometry? GenerateGeometry()
    {
        if (_points.Count < 2) return null;
        var samples = BuildCenterlineSamplesFinal();
        if (samples.Count < 2) return null;

        var geometries = BuildRibbonGeometries(samples, includeStartCap: true, includeEndCap: true);
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

    private List<RibbonGeometry> BuildRibbonGeometries(
        List<StrokePoint> samples,
        bool includeStartCap,
        bool includeEndCap)
    {
        var result = new List<RibbonGeometry>();
        int ribbonCount = ResolveRibbonCount();
        if (ribbonCount <= 1)
        {
            var single = BuildRibbonGeometry(samples, 0, 0, includeStartCap, includeEndCap);
            if (single != null) result.Add(new RibbonGeometry(single, 0));
            return result;
        }

        double centerIndex = (ribbonCount - 1) * 0.5;
        for (int i = 0; i < ribbonCount; i++)
        {
            double ribbonT = centerIndex > 0 ? Math.Abs(i - centerIndex) / centerIndex : 0;
            var ribbonSamples = BuildRibbonSamples(samples, i, ribbonCount);
            var ribbonGeometry = BuildRibbonGeometry(
                ribbonSamples,
                ribbonT,
                i * 17.7,
                includeStartCap,
                includeEndCap);
            if (ribbonGeometry != null)
            {
                result.Add(new RibbonGeometry(ribbonGeometry, ribbonT));
            }
        }

        return result;
    }
}
