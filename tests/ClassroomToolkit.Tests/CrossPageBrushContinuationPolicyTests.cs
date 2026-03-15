using ClassroomToolkit.App.Paint;
using ClassroomToolkit.App.Paint.Brushes;
using FluentAssertions;
using WpfPoint = System.Windows.Point;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageBrushContinuationPolicyTests
{
    [Fact]
    public void Resolve_ShouldFallbackToCurrentInput_WhenPreviousSampleMissing()
    {
        var current = BrushInputSample.CreatePointer(new WpfPoint(120, 220), timestampTicks: 200);

        var decision = CrossPageBrushContinuationPolicy.Resolve(
            current,
            previousInput: null,
            currentPageTop: 0,
            currentPageHeight: 200,
            currentPage: 2,
            targetPage: 3);

        decision.FinalizeSample.Should().Be(current);
        decision.ContinuationSeed.Should().Be(current);
        decision.ShouldReplayCurrentInputAfterResume.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldBridgeAtSeam_WhenCrossingToNextPage()
    {
        var previous = BrushInputSample.CreateStylus(new WpfPoint(100, 190), timestampTicks: 100, pressure: 0.2);
        var current = BrushInputSample.CreateStylus(new WpfPoint(120, 230), timestampTicks: 200, pressure: 0.8);

        var decision = CrossPageBrushContinuationPolicy.Resolve(
            current,
            previous,
            currentPageTop: 0,
            currentPageHeight: 200,
            currentPage: 2,
            targetPage: 3);

        decision.FinalizeSample.Position.Y.Should().BeApproximately(200, 0.001);
        decision.FinalizeSample.Position.X.Should().BeApproximately(105, 0.001);
        decision.FinalizeSample.TimestampTicks.Should().Be(125);
        decision.ContinuationSeed.Position.X.Should().BeApproximately(105, 0.001);
        decision.ContinuationSeed.Position.Y.Should().BeApproximately(200.22, 0.001);
        decision.ContinuationSeed.TimestampTicks.Should().Be(126);
        decision.ShouldReplayCurrentInputAfterResume.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldBridgeAtSeam_WhenCrossingToPreviousPage()
    {
        var previous = BrushInputSample.CreateStylus(new WpfPoint(200, 210), timestampTicks: 400, pressure: 0.6);
        var current = BrushInputSample.CreateStylus(new WpfPoint(180, 170), timestampTicks: 500, pressure: 0.4);

        var decision = CrossPageBrushContinuationPolicy.Resolve(
            current,
            previous,
            currentPageTop: 200,
            currentPageHeight: 300,
            currentPage: 3,
            targetPage: 2);

        decision.FinalizeSample.Position.Y.Should().BeApproximately(200, 0.001);
        decision.FinalizeSample.Position.X.Should().BeApproximately(195, 0.001);
        decision.ContinuationSeed.Position.Y.Should().BeApproximately(199.78, 0.001);
        decision.ShouldReplayCurrentInputAfterResume.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldNotReplay_WhenBridgeAlmostEqualsCurrentInput()
    {
        var previous = BrushInputSample.CreatePointer(new WpfPoint(80, 170), timestampTicks: 100);
        var current = BrushInputSample.CreatePointer(new WpfPoint(90, 180), timestampTicks: 200);

        var decision = CrossPageBrushContinuationPolicy.Resolve(
            current,
            previous,
            currentPageTop: 0,
            currentPageHeight: 179.9,
            currentPage: 2,
            targetPage: 3);

        decision.ShouldReplayCurrentInputAfterResume.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldKeepMinimumSeedOffset_WhenCurrentInputIsNearSeam()
    {
        var previous = BrushInputSample.CreatePointer(new WpfPoint(100, 199.90), timestampTicks: 10);
        var current = BrushInputSample.CreatePointer(new WpfPoint(102, 200.01), timestampTicks: 20);

        var decision = CrossPageBrushContinuationPolicy.Resolve(
            current,
            previous,
            currentPageTop: 0,
            currentPageHeight: 200,
            currentPage: 2,
            targetPage: 3);

        decision.ContinuationSeed.Position.Y.Should().BeApproximately(200.08, 0.001);
    }

    [Fact]
    public void Resolve_ShouldClampFinalizeSampleToSeam_WhenBridgeCannotBeInterpolatedAndCurrentIsBeyondSeam()
    {
        var previous = BrushInputSample.CreatePointer(new WpfPoint(120, 230), timestampTicks: 100);
        var current = BrushInputSample.CreatePointer(new WpfPoint(128, 236), timestampTicks: 110);

        var decision = CrossPageBrushContinuationPolicy.Resolve(
            current,
            previous,
            currentPageTop: 0,
            currentPageHeight: 200,
            currentPage: 2,
            targetPage: 3);

        decision.FinalizeSample.Position.Y.Should().BeApproximately(200, 0.001);
        decision.ContinuationSeed.Position.Y.Should().BeGreaterThan(200);
        decision.ShouldReplayCurrentInputAfterResume.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldNotClamp_WhenBridgeCannotBeInterpolatedButCurrentHasNotCrossedSeam()
    {
        var previous = BrushInputSample.CreatePointer(new WpfPoint(80, 160), timestampTicks: 100);
        var current = BrushInputSample.CreatePointer(new WpfPoint(90, 170), timestampTicks: 110);

        var decision = CrossPageBrushContinuationPolicy.Resolve(
            current,
            previous,
            currentPageTop: 0,
            currentPageHeight: 200,
            currentPage: 2,
            targetPage: 3);

        decision.FinalizeSample.Should().Be(current);
        decision.ContinuationSeed.Should().Be(current);
        decision.ShouldReplayCurrentInputAfterResume.Should().BeFalse();
    }
}
