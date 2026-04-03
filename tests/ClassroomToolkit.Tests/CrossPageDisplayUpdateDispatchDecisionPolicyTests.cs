using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageDisplayUpdateDispatchDecisionPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnSkipPending_WhenSnapshotPending()
    {
        var snapshot = new CrossPageDisplayUpdateDispatchSnapshot(
            Pending: true,
            Panning: true,
            Dragging: false,
            InkOperationActive: false);

        var decision = CrossPageDisplayUpdateDispatchDecisionPolicy.Resolve(
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

        var decision = CrossPageDisplayUpdateDispatchDecisionPolicy.Resolve(
            snapshot,
            elapsedMs: 6,
            draggingMinIntervalMs: 24,
            normalMinIntervalMs: 16);

        decision.Mode.Should().Be(CrossPageDisplayUpdateDispatchMode.Delayed);
        decision.DelayMs.Should().Be(18);
    }
}
