using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;


public sealed class CrossPageDisplayRunGatePolicyTests
{
    [Fact]
    public void Resolve_ShouldAllowRun_WhenDisplayIsActive()
    {
        var decision = CrossPageDisplayRunGatePolicy.Resolve(crossPageDisplayActive: true);

        decision.ShouldRun.Should().BeTrue();
        decision.AbortReason.Should().BeNull();
    }

    [Fact]
    public void Resolve_ShouldBlockRun_WhenDisplayIsInactive()
    {
        var decision = CrossPageDisplayRunGatePolicy.Resolve(crossPageDisplayActive: false);

        decision.ShouldRun.Should().BeFalse();
        decision.AbortReason.Should().Be(CrossPageDeferredDiagnosticReason.Inactive);
    }
}


public sealed class CrossPageDisplayUpdateRunFailureReplayPolicyTests
{
    [Fact]
    public void Resolve_ShouldQueueVisualSyncReplay_ForVisualSyncSource()
    {
        var decision = CrossPageDisplayUpdateRunFailureReplayPolicy.Resolve(
            CrossPageUpdateSources.InkStateChanged);

        decision.QueueVisualSyncReplay.Should().BeTrue();
        decision.QueueInteractionReplay.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldQueueInteractionReplay_ForInteractionSource()
    {
        var decision = CrossPageDisplayUpdateRunFailureReplayPolicy.Resolve(
            CrossPageUpdateSources.PhotoPan);

        decision.QueueVisualSyncReplay.Should().BeFalse();
        decision.QueueInteractionReplay.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldQueueNone_ForBackgroundSource()
    {
        var decision = CrossPageDisplayUpdateRunFailureReplayPolicy.Resolve(
            CrossPageUpdateSources.NeighborRender);

        decision.QueueVisualSyncReplay.Should().BeFalse();
        decision.QueueInteractionReplay.Should().BeFalse();
    }
}

public sealed class CrossPageDisplayUpdateDispatchSnapshotTests
{
    [Fact]
    public void FormatDiagnosticsTag_ShouldMatchExpectedShape()
    {
        var snapshot = new CrossPageDisplayUpdateDispatchSnapshot(
            Pending: true,
            Panning: false,
            Dragging: true,
            InkOperationActive: false);

        var tag = CrossPageDisplayUpdateDispatchSnapshot.FormatDiagnosticsTag(snapshot);

        tag.Should().Be("pending=True panning=False dragging=True");
    }
}
