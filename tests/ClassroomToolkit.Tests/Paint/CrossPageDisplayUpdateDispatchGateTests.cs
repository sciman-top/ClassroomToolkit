using ClassroomToolkit.App.Paint;
using AwesomeAssertions;

namespace ClassroomToolkit.Tests.Paint;


public sealed class CrossPageDisplayRunGatePolicyTests
{
    [Fact]
    public void Resolve_ShouldAllowRun_WhenDisplayIsActive()
    {
        var decision = CrossPageDisplayUpdatePolicies.ResolveCrossPageDisplayRunGate(crossPageDisplayActive: true);

        decision.ShouldRun.Should().BeTrue();
        decision.AbortReason.Should().BeNull();
    }

    [Fact]
    public void Resolve_ShouldBlockRun_WhenDisplayIsInactive()
    {
        var decision = CrossPageDisplayUpdatePolicies.ResolveCrossPageDisplayRunGate(crossPageDisplayActive: false);

        decision.ShouldRun.Should().BeFalse();
        decision.AbortReason.Should().Be(CrossPageDeferredDiagnosticReason.Inactive);
    }
}


public sealed class CrossPageDisplayUpdateRunFailureReplayPolicyTests
{
    [Fact]
    public void Resolve_ShouldQueueVisualSyncReplay_ForVisualSyncSource()
    {
        var decision = CrossPageDisplayUpdatePolicies.ResolveRunFailureReplay(
            CrossPageUpdateSources.InkStateChanged);

        decision.QueueVisualSyncReplay.Should().BeTrue();
        decision.QueueInteractionReplay.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldQueueInteractionReplay_ForInteractionSource()
    {
        var decision = CrossPageDisplayUpdatePolicies.ResolveRunFailureReplay(
            CrossPageUpdateSources.PhotoPan);

        decision.QueueVisualSyncReplay.Should().BeFalse();
        decision.QueueInteractionReplay.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldQueueNone_ForBackgroundSource()
    {
        var decision = CrossPageDisplayUpdatePolicies.ResolveRunFailureReplay(
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
