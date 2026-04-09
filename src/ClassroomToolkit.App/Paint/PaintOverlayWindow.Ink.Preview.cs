using System;
using System.Diagnostics;
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
        if (!_lastBrushInputSample.HasValue)
        {
            _lastBrushInputSample = input;
            return;
        }

        var previous = _lastBrushInputSample.Value;
        var dtMs = (input.TimestampTicks - previous.TimestampTicks) * 1000.0 / Math.Max(Stopwatch.Frequency, 1);
        if (dtMs < InkInputRuntimeDefaults.PredictionUpdateMinDtMs)
        {
            _lastBrushInputSample = input;
            return;
        }

        var dtSeconds = dtMs / 1000.0;
        var v = (input.Position - previous.Position) / Math.Max(dtSeconds, BrushPredictionPreviewDefaults.MinPredictionDtSeconds);
        _lastBrushVelocityDipPerSec = new Vector(
            (_lastBrushVelocityDipPerSec.X * BrushPredictionPreviewDefaults.VelocitySmoothingKeepFactor)
            + (v.X * BrushPredictionPreviewDefaults.VelocitySmoothingApplyFactor),
            (_lastBrushVelocityDipPerSec.Y * BrushPredictionPreviewDefaults.VelocitySmoothingKeepFactor)
            + (v.Y * BrushPredictionPreviewDefaults.VelocitySmoothingApplyFactor));
        _lastBrushInputSample = input;
    }

    private void RenderBrushPreview()
    {
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
        var lead1 = _lastBrushVelocityDipPerSec
            * ((horizonMs * BrushPredictionPreviewDefaults.FirstLeadHorizonRatio) / 1000.0)
            * damping;
        var lead2 = _lastBrushVelocityDipPerSec
            * ((horizonMs * BrushPredictionPreviewDefaults.SecondLeadHorizonRatio) / 1000.0)
            * damping;

        if (lead1.Length > BrushPredictionMaxDistanceDip * BrushPredictionPreviewDefaults.FirstLeadDistanceRatio)
        {
            lead1 *= (BrushPredictionMaxDistanceDip * BrushPredictionPreviewDefaults.FirstLeadDistanceRatio) / lead1.Length;
        }

        if (lead2.Length > BrushPredictionMaxDistanceDip)
        {
            lead2 *= BrushPredictionMaxDistanceDip / lead2.Length;
        }

        var origin = _lastBrushInputSample.Value.Position;
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

    private static void DrawPredictedBrushSegment(
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

        var pen0 = new MediaPen(new SolidColorBrush(c0), w0)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        if (pen0.CanFreeze)
        {
            pen0.Freeze();
        }

        dc.DrawLine(pen0, p0, p1);

        var pen1 = new MediaPen(new SolidColorBrush(c1), w1)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        if (pen1.CanFreeze)
        {
            pen1.Freeze();
        }

        dc.DrawLine(pen1, p1, p2);

        var tipBrush = new SolidColorBrush(c2);
        if (tipBrush.CanFreeze)
        {
            tipBrush.Freeze();
        }

        dc.DrawEllipse(
            tipBrush,
            null,
            p2,
            Math.Max(BrushPredictionPreviewDefaults.MinTipWidthDip, w2 * BrushPredictionPreviewDefaults.TipRadiusRatio),
            Math.Max(BrushPredictionPreviewDefaults.MinTipWidthDip, w2 * BrushPredictionPreviewDefaults.TipRadiusRatio));
    }
}
