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
        // 抬笔链先经此取最终几何，再由核心几何消费方取同一缓存；
        // 避免同一笔几何全量构建两次（EnsureGeometryCache 结果已冻结）。
        EnsureGeometryCache();
        return _cachedCoreGeometry;
    }

    public Geometry? GetLastCoreGeometry()
    {
        EnsureGeometryCache();
        return _cachedCoreGeometry;
    }

    public Geometry? GetPreviewCoreGeometry()
    {
        bool rawPreviewInputUnchanged = !_isActive
            || (_previewCachedRawPositionValid && _previewCachedRawPosition == _lastRawPos);
        if (_cachedPreviewGeometry != null
            && _previewGeometryVersion == _geometryVersion
            && rawPreviewInputUnchanged)
        {
            return _cachedPreviewGeometry;
        }

        if (_points.Count < 2)
        {
            _cachedPreviewGeometry = null;
            _previewGeometryVersion = -1;
            _previewCachedRawPositionValid = false;
            return null;
        }

        bool hasPreviewReleasePoint = TryBuildPreviewReleasePoint(out var previewReleasePoint);
        int previewPointCount = _points.Count + (hasPreviewReleasePoint ? 1 : 0);
        double previewTotalLength = GetCachedPolylineTotalLength();
        if (hasPreviewReleasePoint)
        {
            previewTotalLength += (previewReleasePoint.Position - _points[^1].Position).Length;
        }

        Geometry? preview;
        if (previewPointCount <= PreviewTailPointWindow + 4)
        {
            _previewBaseGeometry = null;
            _previewBasePointCount = 0;
            _previewTailStartGlobalLength = 0.0;
            var source = CopyRangeToPreviewSliceBuffer(
                0,
                previewPointCount,
                hasPreviewReleasePoint,
                previewReleasePoint);
            var samples = BuildCenterlineSamplesFinal(
                source,
                previewFastPath: true,
                globalStartLength: 0.0,
                globalTotalLength: previewTotalLength);
            preview = BuildPreviewCompositeGeometry(samples, includeStartCap: true, includeEndCap: true);
        }
        else
        {
            int basePointCount = Math.Max(2, previewPointCount - PreviewTailPointWindow);
            // 基座只按点数 stride 刷新（与 Marker 一致）；每次 move 仅构建尾窗，
            // 全局长度走缓存，避免每帧 O(n) 全线长计算与全前缀重建。
            bool shouldRefreshBase = _previewBaseGeometry == null
                || _previewBasePointCount <= 0
                || basePointCount < _previewBasePointCount
                || (basePointCount - _previewBasePointCount) >= PreviewBaseRefreshStride
                || Math.Abs(previewTotalLength - _previewBaseGlobalTotalLength)
                    >= Math.Max(2.0, _baseSize * 0.4);

            if (shouldRefreshBase)
            {
                _previewBasePointCount = basePointCount;
                int previewTailStart = Math.Max(0, basePointCount - 3);
                // ComputePolylineLength 的 endExclusive 表示“包含到前一索引”，
                // 因而要得到 source[tailStart] 的弧长，必须把 tailStart+1
                // 个点纳入累计；少一个边段会让尾窗的 progress/taper 滞后一格。
                _previewTailStartGlobalLength = ComputePolylineLength(
                    _points,
                    Math.Min(_points.Count, previewTailStart + 1));
                _previewBaseGlobalTotalLength = previewTotalLength;
                _previewBaseGeometry = BuildPreviewGeometryForRange(
                    0,
                    _previewBasePointCount,
                    includeStartCap: true,
                    includeEndCap: false,
                    globalStartLength: 0.0,
                    globalTotalLength: previewTotalLength,
                    includePreviewReleasePoint: false,
                    previewReleasePoint: default);
                if (_previewBaseGeometry?.CanFreeze == true)
                {
                    _previewBaseGeometry.Freeze();
                }
            }

            int tailStart = Math.Max(0, _previewBasePointCount - 3);
            var tailGeometry = BuildPreviewGeometryForRange(
                tailStart,
                previewPointCount,
                includeStartCap: false,
                includeEndCap: true,
                globalStartLength: _previewTailStartGlobalLength,
                globalTotalLength: previewTotalLength,
                includePreviewReleasePoint: hasPreviewReleasePoint,
                previewReleasePoint: previewReleasePoint);
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
        _previewGeometryVersion = _geometryVersion;
        _previewCachedRawPosition = _lastRawPos;
        _previewCachedRawPositionValid = _isActive && _hasRawPoint;
        return _cachedPreviewGeometry;
    }

    internal Geometry? BuildPredictionGeometry(
        WpfPoint p0,
        WpfPoint p1,
        WpfPoint p2,
        double w0,
        double w1,
        double w2,
        BrushPredictionState state,
        bool includeEndCap = true)
    {
        var samples = new List<StrokePoint>(3)
        {
            new(p0, ClampWidth(w0), progress: 0.0, wetness: state.Wetness, nibAngleRadians: state.NibAngleRadians, nibStrength: state.NibStrength),
            new(p1, ClampWidth(w1), progress: 0.5, wetness: state.Wetness, nibAngleRadians: state.NibAngleRadians, nibStrength: state.NibStrength),
            new(p2, ClampWidth(w2), progress: 1.0, wetness: state.Wetness, nibAngleRadians: state.NibAngleRadians, nibStrength: state.NibStrength)
        };
        var ribbons = BuildRibbonGeometries(samples, includeStartCap: false, includeEndCap: includeEndCap);
        return CombineRibbonGeometries(ribbons);
    }

    internal Geometry? BuildPredictionSegmentGeometry(
        WpfPoint p0,
        WpfPoint p1,
        double w0,
        double w1,
        BrushPredictionState state,
        bool includeEndCap)
    {
        var samples = new List<StrokePoint>(2)
        {
            new(p0, ClampWidth(w0), progress: 0.0, wetness: state.Wetness, nibAngleRadians: state.NibAngleRadians, nibStrength: state.NibStrength),
            new(p1, ClampWidth(w1), progress: 1.0, wetness: state.Wetness, nibAngleRadians: state.NibAngleRadians, nibStrength: state.NibStrength)
        };
        var ribbons = BuildRibbonGeometries(samples, includeStartCap: false, includeEndCap: includeEndCap);
        return CombineRibbonGeometries(ribbons);
    }

    private static Geometry? CombineRibbonGeometries(List<RibbonGeometry> ribbons)
    {
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
        bool includeEndCap,
        double globalStartLength,
        double globalTotalLength,
        bool includePreviewReleasePoint,
        StrokePoint previewReleasePoint)
    {
        int start = Math.Max(0, startInclusive);
        int end = Math.Min(_points.Count, endExclusive);
        bool appendPreviewReleasePoint = includePreviewReleasePoint
            && endExclusive > _points.Count
            && end == _points.Count;
        int count = end - start;
        if (appendPreviewReleasePoint)
        {
            count++;
        }
        if (count < 2)
        {
            return null;
        }

        var source = CopyRangeToPreviewSliceBuffer(
            start,
            end,
            appendPreviewReleasePoint,
            previewReleasePoint);
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

    private double GetCachedPolylineTotalLength()
    {
        if (!_previewPolylineLengthValid)
        {
            _previewPolylineTotalLength = ComputePolylineLength(_points);
            _previewPolylineLengthValid = true;
        }

        return _previewPolylineTotalLength;
    }

    private void TrackAppendedPointLength()
    {
        if (!_previewPolylineLengthValid || _points.Count < 2)
        {
            return;
        }

        var previous = _points[_points.Count - 2].Position;
        var current = _points[_points.Count - 1].Position;
        _previewPolylineTotalLength += (current - previous).Length;
    }

    private void InvalidatePolylineLengthCache()
    {
        _previewPolylineLengthValid = false;
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

    private List<StrokePoint> CopyRangeToPreviewSliceBuffer(
        int startInclusive,
        int endExclusive,
        bool appendPreviewReleasePoint = false,
        StrokePoint previewReleasePoint = default)
    {
        _previewSliceBuffer.Clear();
        int start = Math.Max(0, startInclusive);
        int end = Math.Min(_points.Count, endExclusive);
        for (int i = start; i < end; i++)
        {
            _previewSliceBuffer.Add(_points[i]);
        }
        if (appendPreviewReleasePoint && end == _points.Count)
        {
            _previewSliceBuffer.Add(previewReleasePoint);
        }
        return _previewSliceBuffer;
    }

    private bool TryBuildPreviewReleasePoint(out StrokePoint previewReleasePoint)
    {
        previewReleasePoint = default;
        if (!_isActive || _points.Count == 0)
        {
            return false;
        }

        var last = _points[^1];
        var delta = _lastRawPos - last.Position;
        if (delta.Length <= Math.Max(0.25, _baseSize * 0.02))
        {
            return false;
        }

        double tailFactor = _config.EndTaperStyle == TaperCapStyle.Exposed
            ? Math.Max(0.05, _config.TaperMinWidthFactor * 0.18)
            : Math.Max(0.08, _config.TaperMinWidthFactor * 0.28);
        double minWidth = Math.Clamp(
            _baseSize * tailFactor,
            Math.Max(0.14, _baseSize * 0.015),
            _baseSize * _config.MaxStrokeWidthMultiplier);
        double noisePhase = last.NoisePhase + delta.Length / Math.Max(_baseSize * 0.2, 0.2);
        previewReleasePoint = new StrokePoint(
            _lastRawPos,
            minWidth,
            0,
            0,
            1,
            0,
            noisePhase,
            last.Wetness,
            last.NibAngleRadians,
            last.NibStrength);
        return true;
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
            // 只有核心 ribbon 拥有端点 cap。纹理 ribbon 仍参与墨感，
            // 但不能各自生成尖锋，否则尾端会出现多个分叉/毛刺。
            bool ownsEndpointCaps = i == (ribbonCount / 2);
            var ribbonGeometry = BuildRibbonGeometry(
                ribbonSamples,
                ribbonT,
                i * 17.7,
                includeStartCap && ownsEndpointCaps,
                includeEndCap && ownsEndpointCaps);
            if (ribbonGeometry != null)
            {
                result.Add(new RibbonGeometry(ribbonGeometry, ribbonT));
            }
        }

        return result;
    }
}
