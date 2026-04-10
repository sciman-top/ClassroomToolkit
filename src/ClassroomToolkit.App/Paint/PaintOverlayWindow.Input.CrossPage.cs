using System;
using ClassroomToolkit.App.Paint.Brushes;
using WpfPoint = System.Windows.Point;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private bool ResumeCrossPageInputOperationAfterSwitch(bool switchedPage, BrushInputSample input)
    {
        var pendingSeed = _pendingCrossPageBrushContinuationSample;
        var replayCurrentInput = _pendingCrossPageBrushReplayCurrentInput;
        var seed = pendingSeed ?? input;
        var executionPlan = CrossPageInputResumePolicy.Resolve(
            switchedPage,
            _mode,
            _strokeInProgress,
            _isErasing,
            replayCurrentInput,
            pendingSeed.HasValue,
            seed == input);
        if (executionPlan.ShouldClearPendingBrushState)
        {
            _pendingCrossPageBrushContinuationSample = null;
            _pendingCrossPageBrushReplayCurrentInput = false;
        }

        if (executionPlan.Action == CrossPageInputResumeAction.BeginBrushContinuation)
        {
            // Resume brush continuity after page switch without drawing an intermediate flash frame.
            CapturePointerInput();
            _visualHost.Clear();
            BeginBrushStrokeContinuation(seed, renderInitialPreview: false);
            if (!executionPlan.ShouldUpdateBrushAfterContinuation)
            {
                return false;
            }

            BrushInputSample? lastChangedSample = null;
            AppendCrossPageContinuationSamples(seed, input, ref lastChangedSample);
            if (TryUpdateBrushStrokeGeometry(input))
            {
                lastChangedSample = input;
            }
            if (lastChangedSample.HasValue)
            {
                FlushBrushStrokePreview(lastChangedSample.Value);
            }
            return true;
        }

        if (executionPlan.Action == CrossPageInputResumeAction.BeginEraser)
        {
            BeginEraser(input.Position);
        }

        return false;
    }

    private void AppendCrossPageContinuationSamples(
        BrushInputSample previous,
        BrushInputSample current,
        ref BrushInputSample? lastChangedSample)
    {
        var distance = (current.Position - previous.Position).Length;
        if (distance <= 0.5)
        {
            return;
        }

        long totalTicks = Math.Max(1, current.TimestampTicks - previous.TimestampTicks);
        var segmentCount = Math.Clamp((int)Math.Ceiling(distance / 0.9), 2, 64);
        if (segmentCount <= 2)
        {
            return;
        }

        for (var i = 1; i < segmentCount; i++)
        {
            var t = i / (double)segmentCount;
            if (t >= 1.0)
            {
                break;
            }

            var position = new WpfPoint(
                previous.Position.X + ((current.Position.X - previous.Position.X) * t),
                previous.Position.Y + ((current.Position.Y - previous.Position.Y) * t));
            var timestampTicks = previous.TimestampTicks + Math.Max(1, (long)Math.Round(totalTicks * t));
            var sample = CreateInterpolatedBrushSample(previous, current, position, timestampTicks, t);
            if (TryUpdateBrushStrokeGeometry(sample))
            {
                lastChangedSample = sample;
            }
        }
    }
}
