using FluentAssertions;
using System.Diagnostics;
using System.Windows;
using ClassroomToolkit.App.Paint;
using ClassroomToolkit.App.Paint.Brushes;

namespace ClassroomToolkit.Tests;

public sealed class PaintOverlayPreviewSchedulingContractTests
{
    [Fact]
    public void PredictionVelocity_ShouldAdvanceForDistinctMonotonicSamples()
    {
        long start = Stopwatch.GetTimestamp();
        long step = Math.Max(1, Stopwatch.Frequency / 120);
        var previous = BrushInputSample.CreatePointer(new Point(10, 20), start);
        var current = BrushInputSample.CreatePointer(new Point(30, 24), start + step);

        var velocity = BrushPredictionVelocityPolicy.Resolve(new Vector(), previous, current);

        velocity.X.Should().BeGreaterThan(0);
        velocity.Y.Should().BeGreaterThan(0);
    }

    [Fact]
    public void PredictionVelocity_ShouldIgnoreSamplesWithoutUsableTimeDelta()
    {
        long timestamp = Stopwatch.GetTimestamp();
        var previous = BrushInputSample.CreatePointer(new Point(10, 20), timestamp);
        var current = BrushInputSample.CreatePointer(new Point(30, 24), timestamp);
        var existing = new Vector(12, 4);

        BrushPredictionVelocityPolicy.Resolve(existing, previous, current).Should().Be(existing);
    }

    [Fact]
    public void BrushPreview_ShouldRenderOnCompositionFrame_AndCancelWhenStrokeEnds()
    {
        var source = ContractSourceAggregateLoader.LoadByPattern(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Ink.Preview.cs");
        var flow = ContractSourceAggregateLoader.LoadByPattern(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Ink.BrushFlow.cs");

        source.Should().Contain("CompositionTarget.Rendering += OnBrushPreviewRendering;");
        source.Should().Contain("CompositionTarget.Rendering -= OnBrushPreviewRendering;");
        source.Should().Contain("private void CancelPendingBrushPreview()");
        flow.Should().Contain("if (_brushStyle == PaintBrushStyle.Calligraphy)");
        flow.Should().Contain("RenderBrushPreview();");
        flow.Should().Contain("RequestBrushPreviewRender();");
        flow.Should().Contain("CancelPendingBrushPreview();");
    }

    [Fact]
    public void BrushPrediction_ShouldTrackPreviousSampleSeparatelyFromLatestInput()
    {
        var source = ContractSourceAggregateLoader.LoadByPattern(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Ink.Preview.cs");
        var core = ContractSourceAggregateLoader.LoadByPattern(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Ink.Core.cs");

        core.Should().Contain("private BrushInputSample? _lastBrushPredictionSample;");
        source.Should().Contain("_lastBrushPredictionSample.Value");
        source.Should().Contain("_lastBrushPredictionSample = input;");
    }
}
