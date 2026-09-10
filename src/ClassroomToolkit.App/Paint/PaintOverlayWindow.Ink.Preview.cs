using System;
using System.Windows;
using System.Windows.Media;
using ClassroomToolkit.App.Paint.Brushes;
using MediaColor = System.Windows.Media.Color;
using MediaPen = System.Windows.Media.Pen;
using WpfPoint = System.Windows.Point;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private void UpdateBrushPrediction(BrushInputSample input)
    {
        if (!_lastBrushPredictionSample.HasValue)
        {
            _lastBrushPredictionSample = input;
            return;
        }

        var previous = _lastBrushPredictionSample.Value;
        var previousVelocity = _lastBrushVelocityDipPerSec;
        _lastBrushVelocityDipPerSec = BrushPredictionVelocityPolicy.Resolve(
            _lastBrushVelocityDipPerSec,
            previous,
            input);
        _lastBrushAccelerationDipPerSecSq = BrushPredictionVelocityPolicy.ResolveAcceleration(
            _lastBrushAccelerationDipPerSecSq,
            previous,
            input,
            previousVelocity,
            _lastBrushVelocityDipPerSec);
        _lastBrushPredictionSample = input;
    }

    private void RequestBrushPreviewRender()
    {
        if (_brushPreviewRenderingAttached)
        {
            return;
        }

        _brushPreviewRenderingAttached = true;
        CompositionTarget.Rendering += OnBrushPreviewRendering;
    }

    private void OnBrushPreviewRendering(object? sender, EventArgs e)
    {
        CompositionTarget.Rendering -= OnBrushPreviewRendering;
        _brushPreviewRenderingAttached = false;
        if (!_strokeInProgress || _activeRenderer == null)
        {
            return;
        }

        RenderBrushPreview();
    }

    private void CancelPendingBrushPreview()
    {
        if (!_brushPreviewRenderingAttached)
        {
            return;
        }

        CompositionTarget.Rendering -= OnBrushPreviewRendering;
        _brushPreviewRenderingAttached = false;
    }

    private void RenderBrushPreview()
    {
        BrushInputLatencyTelemetry.RecordPresentedTick(Environment.TickCount);
        if (_activeRenderer == null)
        {
            return;
        }

        _visualHost.UpdateVisual(dc =>
        {
            _activeRenderer.Render(dc);
            if (TryResolvePredictedBrushSegment(
                    out var p0,
                    out var p1,
                    out var p2,
                    out var w0,
                    out var w1,
                    out var w2))
            {
                var previewColor = EffectiveBrushColor();
                DrawPredictedBrushSegment(dc, previewColor, p0, p1, p2, w0, w1, w2);
            }
        });
    }

    private bool TryResolvePredictedBrushSegment(
        out WpfPoint p0,
        out WpfPoint p1,
        out WpfPoint p2,
        out double w0,
        out double w1,
        out double w2)
    {
        p0 = new WpfPoint();
        p1 = new WpfPoint();
        p2 = new WpfPoint();
        w0 = Math.Max(
            BrushPredictionPreviewDefaults.InitialBaseWidthMinDip,
            _brushSize * BrushPredictionPreviewDefaults.InitialBaseWidthFactor);
        w1 = Math.Max(BrushPredictionPreviewDefaults.MinMidWidthDip, w0 * BrushPredictionPreviewDefaults.MidWidthRatio);
        w2 = Math.Max(BrushPredictionPreviewDefaults.MinTipWidthDip, w1 * BrushPredictionPreviewDefaults.InitialTipWidthRatio);

        if (!_strokeInProgress || !_lastBrushInputSample.HasValue)
        {
            return false;
        }

        var speed = _lastBrushVelocityDipPerSec.Length;
        if (speed < BrushPredictionPreviewDefaults.MinSpeedDipPerSec)
        {
            return false;
        }

        double horizonMs = Math.Clamp(_brushPredictionHorizonMs, InkPredictionDefaults.HorizonMinMs, InkPredictionDefaults.HorizonMaxMs);
        var damping = Math.Clamp(
            1.0 - (speed / BrushPredictionPreviewDefaults.DampingSpeedReference),
            BrushPredictionPreviewDefaults.DampingMin,
            1.0);
        double firstLeadSeconds = (horizonMs * BrushPredictionPreviewDefaults.FirstLeadHorizonRatio) / 1000.0;
        double secondLeadSeconds = (horizonMs * BrushPredictionPreviewDefaults.SecondLeadHorizonRatio) / 1000.0;
        double accelerationGain = damping * BrushPredictionPreviewDefaults.AccelerationLeadGain;
        var lead1 = (_lastBrushVelocityDipPerSec * firstLeadSeconds * damping)
            + (_lastBrushAccelerationDipPerSecSq * (0.5 * firstLeadSeconds * firstLeadSeconds) * accelerationGain);
        var lead2 = (_lastBrushVelocityDipPerSec * secondLeadSeconds * damping)
            + (_lastBrushAccelerationDipPerSecSq * (0.5 * secondLeadSeconds * secondLeadSeconds) * accelerationGain);

        if (lead1.Length > BrushPredictionMaxDistanceDip * BrushPredictionPreviewDefaults.FirstLeadDistanceRatio)
        {
            lead1 *= (BrushPredictionMaxDistanceDip * BrushPredictionPreviewDefaults.FirstLeadDistanceRatio) / lead1.Length;
        }

        if (lead2.Length > BrushPredictionMaxDistanceDip)
        {
            lead2 *= BrushPredictionMaxDistanceDip / lead2.Length;
        }

        // 预测段从滤波后的可见笔尖出发，避免转向时预测尖与墨迹脱节。
        var origin = _activeRenderer != null && _activeRenderer.TryGetTipPosition(out var tipPosition)
            ? tipPosition
            : _lastBrushInputSample.Value.Position;
        p0 = origin;
        p1 = origin + lead1;
        p2 = origin + lead2;
        double speedFactor = Math.Clamp(
            (speed - BrushPredictionPreviewDefaults.MinSpeedDipPerSec) / BrushPredictionPreviewDefaults.SpeedFactorRange,
            0.0,
            1.0);
        var baseWidth = Math.Max(
            BrushPredictionPreviewDefaults.MinBaseWidthDip,
            _brushSize * (BrushPredictionPreviewDefaults.BaseWidthFactor + speedFactor * BrushPredictionPreviewDefaults.SpeedWidthGainFactor));
        w0 = baseWidth;
        w1 = Math.Max(BrushPredictionPreviewDefaults.MinMidWidthDip, baseWidth * BrushPredictionPreviewDefaults.MidWidthRatio);
        w2 = Math.Max(BrushPredictionPreviewDefaults.MinTipWidthDip, baseWidth * BrushPredictionPreviewDefaults.TipWidthRatio);
        return true;
    }

    private void DrawPredictedBrushSegment(
        DrawingContext dc,
        MediaColor color,
        WpfPoint p0,
        WpfPoint p1,
        WpfPoint p2,
        double w0,
        double w1,
        double w2)
    {
        byte a0 = (byte)Math.Clamp(
            color.A * BrushPredictionPreviewDefaults.PrimaryAlphaMultiplier,
            InkPredictionDefaults.PrimaryAlphaMin,
            InkPredictionDefaults.PrimaryAlphaMax);
        byte a1 = (byte)Math.Clamp(
            color.A * BrushPredictionPreviewDefaults.SecondaryAlphaMultiplier,
            InkPredictionDefaults.SecondaryAlphaMin,
            InkPredictionDefaults.SecondaryAlphaMax);
        byte a2 = (byte)Math.Clamp(
            color.A * BrushPredictionPreviewDefaults.TipAlphaMultiplier,
            InkPredictionDefaults.TipAlphaMin,
            InkPredictionDefaults.TipAlphaMax);

        var c0 = MediaColor.FromArgb(a0, color.R, color.G, color.B);
        var c1 = MediaColor.FromArgb(a1, color.R, color.G, color.B);
        var c2 = MediaColor.FromArgb(a2, color.R, color.G, color.B);

        if (_activeRenderer is VariableWidthBrushRenderer calligraphyRenderer)
        {
            // 毛笔预测沿用同一轮廓生成器，但按“主体 / 尾段 / 尖端”分层
            // 绘制；否则 c1、c2 会被计算后完全丢弃，预测段只有一种透明度。
            var predictionGeometry = calligraphyRenderer.BuildPredictionGeometry(
                p0,
                p1,
                p2,
                w0,
                w1,
                w2,
                includeEndCap: false);
            if (predictionGeometry != null)
            {
                dc.DrawGeometry(GetCachedSolidBrush(c0), null, predictionGeometry);
                var tailGeometry = calligraphyRenderer.BuildPredictionSegmentGeometry(
                    p1,
                    p2,
                    w1,
                    w2,
                    includeEndCap: true);
                if (tailGeometry != null)
                {
                    dc.DrawGeometry(GetCachedSolidBrush(c1), null, tailGeometry);
                }

                dc.DrawEllipse(
                    GetCachedSolidBrush(c2),
                    null,
                    p2,
                    Math.Max(BrushPredictionPreviewDefaults.MinTipWidthDip, w2 * BrushPredictionPreviewDefaults.TipRadiusRatio),
                    Math.Max(BrushPredictionPreviewDefaults.MinTipWidthDip, w2 * BrushPredictionPreviewDefaults.TipRadiusRatio));
                return;
            }
        }

        var pen0 = GetCachedPen(c0, w0);

        dc.DrawLine(pen0, p0, p1);

        var pen1 = GetCachedPen(c1, w1);

        dc.DrawLine(pen1, p1, p2);

        var tipBrush = GetCachedSolidBrush(c2);

        dc.DrawEllipse(
            tipBrush,
            null,
            p2,
            Math.Max(BrushPredictionPreviewDefaults.MinTipWidthDip, w2 * BrushPredictionPreviewDefaults.TipRadiusRatio),
            Math.Max(BrushPredictionPreviewDefaults.MinTipWidthDip, w2 * BrushPredictionPreviewDefaults.TipRadiusRatio));
    }
}
