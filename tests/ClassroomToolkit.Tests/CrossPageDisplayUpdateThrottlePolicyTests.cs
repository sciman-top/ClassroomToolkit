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

    [Theory]
    [InlineData(true, false, 5.1, 24, 23)]
    [InlineData(false, true, 10.0, 24, 18)]
    public void Resolve_ShouldReturnDelayed_WhenThrottleActiveAndElapsedNotEnough(
        bool photoPanning,
        bool crossPageDragging,
        double elapsedMs,
        int draggingMinIntervalMs,
        int expectedDelay)
    {
        var decision = CrossPageDisplayUpdateThrottlePolicy.Resolve(
            updatePending: false,
            photoPanning: photoPanning,
            crossPageDragging: crossPageDragging,
            inkOperationActive: false,
            elapsedMs: elapsedMs,
            draggingMinIntervalMs: draggingMinIntervalMs,
            normalMinIntervalMs: 16);

        decision.Mode.Should().Be(CrossPageDisplayUpdateDispatchMode.Delayed);
        decision.DelayMs.Should().Be(expectedDelay);
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
            elapsedMs: 28,
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
