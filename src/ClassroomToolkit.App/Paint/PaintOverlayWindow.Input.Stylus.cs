using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Paint.Brushes;
using WpfPoint = System.Windows.Point;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private void OnStylusDown(object sender, StylusDownEventArgs e)
    {
        var handledByPhotoPan = !_photoLoading && TryHandleStylusPhotoPan(e, StylusPhotoPanPhase.Down);
        var shouldIgnoreFromPhotoControls = ShouldIgnoreInputFromPhotoControls(e.OriginalSource as DependencyObject);
        var stylusPoints = e.GetStylusPoints(OverlayRoot);
        var executionPlan = StylusDownExecutionPolicy.Resolve(
            _photoLoading,
            handledByPhotoPan,
            shouldIgnoreFromPhotoControls,
            stylusPoints.Count > 0);
        if (executionPlan.Action == StylusDownExecutionAction.None)
        {
            return;
        }

        if (executionPlan.ShouldResetTimestampState)
        {
            StylusSampleTimestampStateUpdater.Reset(ref _stylusSampleTimestampState);
        }

        switch (executionPlan.Action)
        {
            case StylusDownExecutionAction.HandleFirstStylusPoint:
            {
                long nowTicks = Stopwatch.GetTimestamp();
                var input = CreateStylusInputSample(stylusPoints[0], nowTicks);
                RememberStylusSampleTimestamp(input.TimestampTicks);
                HandlePointerDown(input);
                break;
            }
            case StylusDownExecutionAction.HandlePointerPosition:
                HandlePointerDown(e.GetPosition(OverlayRoot));
                break;
        }

        if (executionPlan.ShouldMarkHandled)
        {
            e.Handled = true;
        }
    }

    private void OnStylusMove(object sender, StylusEventArgs e)
    {
        var handledByPhotoPan = !_photoLoading && TryHandleStylusPhotoPan(e, StylusPhotoPanPhase.Move);
        var stylusPoints = e.GetStylusPoints(OverlayRoot);
        var interactionState = CaptureInputInteractionState();
        var executionPlan = StylusMoveExecutionPolicy.Resolve(
            _photoLoading,
            handledByPhotoPan,
            IsInkOperationActive(),
            stylusPoints.Count > 0,
            _mode,
            _strokeInProgress,
            interactionState.CrossPageDisplayActive);
        switch (executionPlan.Action)
        {
            case StylusMoveExecutionAction.None:
                return;
            case StylusMoveExecutionAction.HandlePointerPosition:
                HandlePointerMove(e.GetPosition(OverlayRoot));
                break;
            case StylusMoveExecutionAction.HandleBrushBatch:
                HandleStylusBrushMoveBatch(stylusPoints);
                break;
            case StylusMoveExecutionAction.HandleStylusPointsIndividually:
                HandleStylusPointsIndividually(stylusPoints);
                break;
        }

        if (executionPlan.ShouldMarkHandled)
        {
            e.Handled = true;
        }
    }

    private void HandleStylusBrushMoveBatch(StylusPointCollection stylusPoints)
    {
        MarkInkInput();
        long nowTicks = Stopwatch.GetTimestamp();
        long spanTicks = ResolveStylusBatchSpanTicks(nowTicks, stylusPoints.Count);
        long stepTicks = StylusBatchDispatchPolicy.ResolveStepTicks(spanTicks, stylusPoints.Count);
        long batchStartTicks = StylusBatchDispatchPolicy.ResolveBatchStartTicks(nowTicks, stepTicks, stylusPoints.Count);
        BrushInputSample? lastChangedSample = null;
        BrushInputSample? previousSample = (_lastPointerPosition.HasValue && _stylusSampleTimestampState.HasTimestamp)
            ? BrushInputSample.CreatePointer(_lastPointerPosition.Value, _stylusSampleTimestampState.LastTimestampTicks)
            : null;

        for (int index = 0; index < stylusPoints.Count; index++)
        {
            var stylusPoint = stylusPoints[index];
            long timestampTicks = EnsureMonotonicStylusTimestamp(batchStartTicks + (stepTicks * index));
            var sample = CreateStylusInputSample(stylusPoint, timestampTicks);
            if (previousSample.HasValue)
            {
                AppendInterpolatedBrushSamples(previousSample.Value, sample, ref lastChangedSample);
            }

            _lastPointerPosition = sample.Position;
            if (TryUpdateBrushStrokeGeometry(sample))
            {
                lastChangedSample = sample;
            }
            previousSample = sample;
            RememberStylusSampleTimestamp(sample.TimestampTicks);
        }

        if (lastChangedSample.HasValue)
        {
            FlushBrushStrokePreview(lastChangedSample.Value);
        }
    }

    private void HandleStylusPointsIndividually(StylusPointCollection stylusPoints)
    {
        long nowTicks = Stopwatch.GetTimestamp();
        long spanTicks = ResolveStylusBatchSpanTicks(nowTicks, stylusPoints.Count);
        long stepTicks = StylusBatchDispatchPolicy.ResolveStepTicks(spanTicks, stylusPoints.Count);
        long batchStartTicks = StylusBatchDispatchPolicy.ResolveBatchStartTicks(nowTicks, stepTicks, stylusPoints.Count);

        for (int index = 0; index < stylusPoints.Count; index++)
        {
            var stylusPoint = stylusPoints[index];
            long timestampTicks = EnsureMonotonicStylusTimestamp(batchStartTicks + (stepTicks * index));
            var sample = CreateStylusInputSample(stylusPoint, timestampTicks);
            HandlePointerMove(sample);
            RememberStylusSampleTimestamp(sample.TimestampTicks);
        }
    }

    private void OnStylusUp(object sender, StylusEventArgs e)
    {
        var handledByPhotoPan = !_photoLoading && TryHandleStylusPhotoPan(e, StylusPhotoPanPhase.Up);
        var stylusPoints = e.GetStylusPoints(OverlayRoot);
        var executionPlan = StylusUpExecutionPolicy.Resolve(
            _photoLoading,
            handledByPhotoPan,
            IsInkOperationActive(),
            stylusPoints.Count > 0);
        switch (executionPlan.Action)
        {
            case StylusUpExecutionAction.None:
                return;
            case StylusUpExecutionAction.HandleLastStylusPoint:
            {
                long nowTicks = Stopwatch.GetTimestamp();
                long timestampTicks = EnsureMonotonicStylusTimestamp(nowTicks);
                var input = CreateStylusInputSample(stylusPoints[^1], timestampTicks);
                RememberStylusSampleTimestamp(input.TimestampTicks);
                HandlePointerUp(input);
                break;
            }
            case StylusUpExecutionAction.HandlePointerPosition:
                HandlePointerUp(e.GetPosition(OverlayRoot));
                break;
        }

        if (executionPlan.ShouldMarkHandled)
        {
            e.Handled = true;
        }
    }

    private bool TryHandleStylusPhotoPan(StylusEventArgs e, StylusPhotoPanPhase phase)
    {
        var interactionState = CaptureInputInteractionState();
        var shouldPanPhoto = ResolveShouldPanPhoto(interactionState);
        var panDecision = StylusPhotoPanRoutingPolicy.Resolve(
            shouldPanPhoto,
            _photoPanning,
            phase);

        var pointerSourcePlan = panDecision == StylusPhotoPanRoutingDecision.BeginPan
            ? ResolvePointerSourceHandlingPlan(e)
            : new OverlayPointerSourceHandlingPlan(
                ShouldContinue: true,
                ShouldMarkHandled: false,
                ShouldHideEraserPreview: false);
        var executionPlan = StylusPhotoPanExecutionPolicy.Resolve(
            panDecision,
            pointerSourcePlan.ShouldContinue,
            pointerSourcePlan.ShouldMarkHandled,
            PhotoPanBeginGuardPolicy.ShouldBegin(shouldPanPhoto, _photoPanning));
        if (executionPlan.ShouldMarkHandled)
        {
            e.Handled = true;
        }

        switch (executionPlan.Action)
        {
            case StylusPhotoPanExecutionAction.PassThrough:
                return false;
            case StylusPhotoPanExecutionAction.ReturnWithoutPan:
                return true;
            case StylusPhotoPanExecutionAction.BeginPan:
                BeginPhotoPan(e.GetPosition(OverlayRoot), captureStylus: true);
                return true;
            case StylusPhotoPanExecutionAction.UpdatePan:
                UpdatePhotoPan(e.GetPosition(OverlayRoot));
                return true;
            case StylusPhotoPanExecutionAction.EndPan:
                EndPhotoPan();
                return true;
            default:
                return true;
        }
    }

    private InputInteractionState CaptureInputInteractionState()
    {
        return InputInteractionStatePolicy.Resolve(
            photoModeActive: _photoModeActive,
            boardActive: IsBoardActive(),
            crossPageDisplayEnabled: IsCrossPageDisplaySettingEnabled());
    }

    private long ResolveStylusBatchSpanTicks(long nowTicks, int sampleCount)
    {
        return StylusSampleTimestampPolicy.ResolveBatchSpanTicks(
            Stopwatch.Frequency,
            nowTicks,
            sampleCount,
            _stylusSampleTimestampState);
    }

    private long EnsureMonotonicStylusTimestamp(long timestampTicks)
    {
        return StylusSampleTimestampPolicy.EnsureMonotonicTimestamp(
            timestampTicks,
            _stylusSampleTimestampState);
    }

    private void RememberStylusSampleTimestamp(long timestampTicks)
    {
        StylusSampleTimestampStateUpdater.Remember(
            ref _stylusSampleTimestampState,
            timestampTicks);
    }

    private void AppendInterpolatedBrushSamples(
        BrushInputSample previous,
        BrushInputSample current,
        ref BrushInputSample? lastChangedSample)
    {
        double distance = (current.Position - previous.Position).Length;
        long totalTicks = Math.Max(
            StylusInterpolationDefaults.MinTimestampStepTicks,
            current.TimestampTicks - previous.TimestampTicks);
        double dtMs = totalTicks * 1000.0 / Math.Max(Stopwatch.Frequency, 1);
        double speedDipPerMs = distance / Math.Max(StylusInterpolationDefaults.MinDtMsForSpeed, dtMs);
        double interpolationStepDip = StylusInterpolationPolicy.ResolveInterpolationStepDip(
            _brushSize,
            distance,
            totalTicks,
            Stopwatch.Frequency);
        if (!StylusInterpolationPolicy.ShouldInterpolate(distance, interpolationStepDip))
        {
            return;
        }

        int maxSegments = StylusInterpolationPolicy.ResolveMaxSegments(speedDipPerMs, dtMs);

        int segmentCount = Math.Clamp(
            (int)Math.Ceiling(distance / interpolationStepDip),
            StylusInterpolationDefaults.MinSegmentCount,
            maxSegments);
        if (segmentCount <= StylusInterpolationDefaults.MinSegmentCount)
        {
            return;
        }

        for (int i = 1; i < segmentCount; i++)
        {
            double t = i / (double)segmentCount;
            if (t >= StylusInterpolationDefaults.SegmentProgressUpperBound)
            {
                break;
            }

            var position = new WpfPoint(
                previous.Position.X + ((current.Position.X - previous.Position.X) * t),
                previous.Position.Y + ((current.Position.Y - previous.Position.Y) * t));
            long timestampTicks = previous.TimestampTicks + Math.Max(
                StylusInterpolationDefaults.MinTimestampStepTicks,
                (long)Math.Round(totalTicks * t));
            timestampTicks = EnsureMonotonicStylusTimestamp(timestampTicks);
            var sample = CreateInterpolatedBrushSample(previous, current, position, timestampTicks, t);
            if (TryUpdateBrushStrokeGeometry(sample))
            {
                lastChangedSample = sample;
            }
            RememberStylusSampleTimestamp(sample.TimestampTicks);
        }
    }

    private static BrushInputSample CreateInterpolatedBrushSample(
        BrushInputSample previous,
        BrushInputSample current,
        WpfPoint position,
        long timestampTicks,
        double t)
    {
        if (!previous.HasPressure || !current.HasPressure)
        {
            return BrushInputSample.CreatePointer(
                position,
                timestampTicks,
                LerpNullable(previous.AzimuthRadians, current.AzimuthRadians, t),
                LerpNullable(previous.AltitudeRadians, current.AltitudeRadians, t),
                LerpNullable(previous.TiltXRadians, current.TiltXRadians, t),
                LerpNullable(previous.TiltYRadians, current.TiltYRadians, t));
        }

        double pressure = previous.Pressure + ((current.Pressure - previous.Pressure) * t);
        return BrushInputSample.CreateStylus(
            position,
            timestampTicks,
            pressure,
            LerpNullable(previous.AzimuthRadians, current.AzimuthRadians, t),
            LerpNullable(previous.AltitudeRadians, current.AltitudeRadians, t),
            LerpNullable(previous.TiltXRadians, current.TiltXRadians, t),
            LerpNullable(previous.TiltYRadians, current.TiltYRadians, t));
    }

    private static double? LerpNullable(double? a, double? b, double t)
    {
        if (!a.HasValue && !b.HasValue)
        {
            return null;
        }

        double from = a ?? b ?? 0.0;
        double to = b ?? a ?? 0.0;
        return from + ((to - from) * t);
    }
}
