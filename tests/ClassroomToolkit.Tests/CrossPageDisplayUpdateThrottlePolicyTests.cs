using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageDisplayUpdateThrottlePolicyTests
{
    [Fact]
    public void Resolve_ShouldSkip_WhenPendingAlreadyTrue()
    {
        var decision = CrossPageDisplayUpdateThrottlePolicy.Resolve(
            updatePending: true,
            photoPanning: true,
            crossPageDragging: true,
            inkOperationActive: false,
            elapsedMs: 0,
            draggingMinIntervalMs: 24,
            normalMinIntervalMs: 16);

        decision.Mode.Should().Be(CrossPageDisplayUpdateDispatchMode.SkipPending);
        decision.DelayMs.Should().Be(CrossPageDisplayUpdateThrottleDefaults.ImmediateDelayMs);
    }

    [Fact]
    public void Resolve_ShouldReturnDelayed_WhenPhotoPanThrottleActiveAndElapsedNotEnough()
    {
        var decision = CrossPageDisplayUpdateThrottlePolicy.Resolve(
            updatePending: false,
            photoPanning: true,
            crossPageDragging: false,
            inkOperationActive: false,
            elapsedMs: 5.1,
            draggingMinIntervalMs: 24,
            normalMinIntervalMs: 16);

        decision.Mode.Should().Be(CrossPageDisplayUpdateDispatchMode.Delayed);
        decision.DelayMs.Should().Be(CrossPageDisplayUpdateMinIntervalThresholds.PanOnlyMinMs - 5);
    }

    [Fact]
    public void Resolve_ShouldReturnDelayed_WhenCrossPageDragThrottleActiveAndElapsedNotEnough()
    {
        var decision = CrossPageDisplayUpdateThrottlePolicy.Resolve(
            updatePending: false,
            photoPanning: false,
            crossPageDragging: true,
            inkOperationActive: false,
            elapsedMs: 10.0,
            draggingMinIntervalMs: 24,
            normalMinIntervalMs: 16);

        decision.Mode.Should().Be(CrossPageDisplayUpdateDispatchMode.Delayed);
        decision.DelayMs.Should().Be(CrossPageDisplayUpdateMinIntervalThresholds.PanOnlyMinMs - 10);
    }

    [Fact]
    public void Resolve_ShouldReturnDirect_WhenThrottleInactive()
    {
        var decision = CrossPageDisplayUpdateThrottlePolicy.Resolve(
            updatePending: false,
            photoPanning: false,
            crossPageDragging: false,
            inkOperationActive: false,
            elapsedMs: 0,
            draggingMinIntervalMs: 24,
            normalMinIntervalMs: 16);

        decision.Mode.Should().Be(CrossPageDisplayUpdateDispatchMode.Direct);
        decision.DelayMs.Should().Be(CrossPageDisplayUpdateThrottleDefaults.ImmediateDelayMs);
    }

    [Fact]
    public void Resolve_ShouldReturnDirect_WhenThrottleActiveButElapsedEnough()
    {
        var decision = CrossPageDisplayUpdateThrottlePolicy.Resolve(
            updatePending: false,
            photoPanning: true,
            crossPageDragging: false,
            inkOperationActive: false,
            elapsedMs: CrossPageDisplayUpdateMinIntervalThresholds.PanOnlyMinMs,
            draggingMinIntervalMs: 24,
            normalMinIntervalMs: 16);

        decision.Mode.Should().Be(CrossPageDisplayUpdateDispatchMode.Direct);
        decision.DelayMs.Should().Be(CrossPageDisplayUpdateThrottleDefaults.ImmediateDelayMs);
    }

    [Fact]
    public void Resolve_ShouldReturnDelayed_WhenInkOperationActiveAndElapsedNotEnough()
    {
        var decision = CrossPageDisplayUpdateThrottlePolicy.Resolve(
            updatePending: false,
            photoPanning: false,
            crossPageDragging: false,
            inkOperationActive: true,
            elapsedMs: 4,
            draggingMinIntervalMs: 24,
            normalMinIntervalMs: 16);

        decision.Mode.Should().Be(CrossPageDisplayUpdateDispatchMode.Delayed);
        decision.DelayMs.Should().Be(CrossPageDisplayUpdateMinIntervalThresholds.InkOnlyMinMs - 4);
    }

    [Fact]
    public void Resolve_ShouldUseWiderDelay_WhenPanAndInkActive()
    {
        var decision = CrossPageDisplayUpdateThrottlePolicy.Resolve(
            updatePending: false,
            photoPanning: true,
            crossPageDragging: false,
            inkOperationActive: true,
            elapsedMs: 6,
            draggingMinIntervalMs: 24,
            normalMinIntervalMs: 16);

        decision.Mode.Should().Be(CrossPageDisplayUpdateDispatchMode.Delayed);
        decision.DelayMs.Should().Be(CrossPageDisplayUpdateMinIntervalThresholds.PanInkActiveMinMs - 6);
    }
}
