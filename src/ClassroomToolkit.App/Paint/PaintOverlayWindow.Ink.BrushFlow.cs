using System;
using System.Windows;
using ClassroomToolkit.App.Paint.Brushes;
using WpfPoint = System.Windows.Point;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private void BeginBrushStroke(BrushInputSample input)
    {
        var position = input.Position;
        EnsureActiveRenderer();
        if (_activeRenderer == null)
        {
            return;
        }
        PushHistory();
        CaptureStrokeContext();
        _strokeInProgress = true;
        _activeBrushStrokeUsesCrossPageContinuation = false;
        var color = EffectiveBrushColor();
        _activeRenderer.Initialize(color, _brushSize, color.A);
        _activeRenderer.OnDown(input);
        _lastBrushInputSample = input;
        _lastBrushVelocityDipPerSec = new Vector(0, 0);
        RenderBrushPreview();
        _lastCalligraphyPreviewUtc = GetCurrentUtcTimestamp();
        _lastCalligraphyPreviewPoint = position;
    }

    private void BeginBrushStrokeContinuation(BrushInputSample input, bool renderInitialPreview)
    {
        var position = input.Position;
        EnsureActiveRenderer();
        if (_activeRenderer == null)
        {
            return;
        }

        _strokeInProgress = true;
        _activeBrushStrokeUsesCrossPageContinuation = true;
        var color = EffectiveBrushColor();
        _activeRenderer.Initialize(color, _brushSize, color.A);
        _activeRenderer.OnDown(input);
        _lastBrushInputSample = input;
        _lastBrushVelocityDipPerSec = new Vector(0, 0);
        if (renderInitialPreview)
        {
            RenderBrushPreview();
        }
        _lastCalligraphyPreviewUtc = GetCurrentUtcTimestamp();
        _lastCalligraphyPreviewPoint = position;
    }

    private void UpdateBrushStroke(BrushInputSample input)
    {
        if (!TryUpdateBrushStrokeGeometry(input))
        {
            return;
        }
        FlushBrushStrokePreview(input);
    }

    private bool TryUpdateBrushStrokeGeometry(BrushInputSample input)
    {
        if (!_strokeInProgress || _activeRenderer == null)
        {
            return false;
        }

        var versionBeforeMove = _activeRenderer.GeometryVersion;
        _activeRenderer.OnMove(input);
        // Keep seam-bridge continuation input aligned with the latest accepted move sample.
        _lastBrushInputSample = input;
        return _activeRenderer.GeometryVersion != versionBeforeMove;
    }

    private void FlushBrushStrokePreview(BrushInputSample input)
    {
        var position = input.Position;
        if (_brushStyle == PaintBrushStyle.Calligraphy && ShouldThrottleCalligraphyPreview(position))
        {
            return;
        }

        UpdateBrushPrediction(input);
        RenderBrushPreview();
    }

    private void EndBrushStroke(BrushInputSample input)
    {
        if (!_strokeInProgress || _activeRenderer == null)
        {
            return;
        }
        var position = input.Position;
        _activeRenderer.OnUp(input);
        var geometry = _activeRenderer.GetLastStrokeGeometry();
        if (geometry != null)
        {
            CommitGeometryFill(geometry, EffectiveBrushColor());
            RecordBrushStroke(geometry);
        }
        _activeRenderer.Reset();
        _visualHost.Clear();
        _strokeInProgress = false;
        var usedCrossPageContinuation = _activeBrushStrokeUsesCrossPageContinuation;
        _activeBrushStrokeUsesCrossPageContinuation = false;
        _lastBrushInputSample = null;
        _lastBrushVelocityDipPerSec = new Vector(0, 0);
        _lastCalligraphyPreviewPoint = null;
        var photoInkModeActive = IsPhotoInkModeActive();
        if (!_suppressImmediatePhotoInkRedraw
            && PhotoInkRenderPolicy.ShouldRequestImmediateRedraw(
                photoInkModeActive,
                RasterImage.RenderTransform,
                _photoContentTransform,
                usedCrossPageContinuation))
        {
            RequestInkRedraw();
        }
        SetInkContextDirty();
    }

    private void BeginBrushStroke(WpfPoint position)
    {
        BeginBrushStroke(BrushInputSample.CreatePointer(position));
    }

    private void UpdateBrushStroke(WpfPoint position)
    {
        UpdateBrushStroke(BrushInputSample.CreatePointer(position));
    }

    private void EndBrushStroke(WpfPoint position)
    {
        EndBrushStroke(BrushInputSample.CreatePointer(position));
    }

    private bool ShouldThrottleCalligraphyPreview(WpfPoint position)
    {
        if (_lastCalligraphyPreviewPoint.HasValue)
        {
            var delta = position - _lastCalligraphyPreviewPoint.Value;
            if (delta.Length >= _calligraphyPreviewMinDistance)
            {
                _lastCalligraphyPreviewPoint = position;
                _lastCalligraphyPreviewUtc = GetCurrentUtcTimestamp();
                return false;
            }
        }
        var nowUtc = GetCurrentUtcTimestamp();
        if ((nowUtc - _lastCalligraphyPreviewUtc).TotalMilliseconds >= CalligraphyPreviewMinIntervalMs)
        {
            _lastCalligraphyPreviewUtc = nowUtc;
            _lastCalligraphyPreviewPoint = position;
            return false;
        }
        return true;
    }
}
