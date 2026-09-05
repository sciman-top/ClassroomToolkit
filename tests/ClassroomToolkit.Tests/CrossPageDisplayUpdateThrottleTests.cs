using ClassroomToolkit.App.Paint;
using FluentAssertions;

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
        decision.DelayMs.Should().Be(19);
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
        decision.DelayMs.Should().Be(14);
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
            elapsedMs: 24,
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
        decision.DelayMs.Should().Be(20);
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
        decision.DelayMs.Should().Be(18);
    }
}


public sealed class CrossPageDisplayUpdateMinIntervalPolicyTests
{
    [Fact]
    public void ResolveMs_ShouldUsePanInterval_WhenPanningActive()
    {
        var value = CrossPageDisplayUpdateMinIntervalPolicy.ResolveMs(
            photoPanning: true,
            crossPageDragging: false,
            inkOperationActive: false,
            draggingMinIntervalMs: 24,
            normalMinIntervalMs: 16);

        value.Should().Be(24);
    }

    [Fact]
    public void ResolveMs_ShouldUseInkInterval_WhenOnlyInkActive()
    {
        var value = CrossPageDisplayUpdateMinIntervalPolicy.ResolveMs(
            photoPanning: false,
            crossPageDragging: false,
            inkOperationActive: true,
            draggingMinIntervalMs: 24,
            normalMinIntervalMs: 16);

        value.Should().Be(24);
    }

    [Fact]
    public void ResolveMs_ShouldUseWiderInterval_WhenPanAndInkActive()
    {
        var value = CrossPageDisplayUpdateMinIntervalPolicy.ResolveMs(
            photoPanning: true,
            crossPageDragging: false,
            inkOperationActive: true,
            draggingMinIntervalMs: 24,
            normalMinIntervalMs: 16);

        value.Should().Be(24);
    }

    [Fact]
    public void ResolveMs_ShouldUseNormalInterval_WhenNoInteraction()
    {
        var value = CrossPageDisplayUpdateMinIntervalPolicy.ResolveMs(
            photoPanning: false,
            crossPageDragging: false,
            inkOperationActive: false,
            draggingMinIntervalMs: 24,
            normalMinIntervalMs: 16);

        value.Should().Be(16);
    }
}


public sealed class CrossPageDisplayUpdateMinIntervalThresholdsTests
{
    [Fact]
    public void Thresholds_ShouldMatchResponsiveValues()
    {
        CrossPageDisplayUpdateMinIntervalThresholds.PanInkActiveMinMs.Should().Be(24);
        CrossPageDisplayUpdateMinIntervalThresholds.PanOnlyMinMs.Should().Be(20);
        CrossPageDisplayUpdateMinIntervalThresholds.InkOnlyMinMs.Should().Be(16);
    }
}

public sealed class CrossPageDisplayUpdateThrottleSnapshotOverloadTests
{
    [Fact]
    public void Resolve_ShouldReturnSkipPending_WhenSnapshotPending()
    {
        var snapshot = new CrossPageDisplayUpdateDispatchSnapshot(
            Pending: true,
            Panning: true,
            Dragging: false,
            InkOperationActive: false);

        var decision = CrossPageDisplayUpdateThrottlePolicy.Resolve(
            snapshot,
            elapsedMs: 0,
            draggingMinIntervalMs: 24,
            normalMinIntervalMs: 16);

        decision.Mode.Should().Be(CrossPageDisplayUpdateDispatchMode.SkipPending);
    }

    [Fact]
    public void Resolve_ShouldReturnDelayed_WhenInteractionActiveAndElapsedInsufficient()
    {
        var snapshot = new CrossPageDisplayUpdateDispatchSnapshot(
            Pending: false,
            Panning: true,
            Dragging: false,
            InkOperationActive: false);

        var decision = CrossPageDisplayUpdateThrottlePolicy.Resolve(
            snapshot,
            elapsedMs: 6,
            draggingMinIntervalMs: 24,
            normalMinIntervalMs: 16);

        decision.Mode.Should().Be(CrossPageDisplayUpdateDispatchMode.Delayed);
        decision.DelayMs.Should().Be(18);
    }
}
