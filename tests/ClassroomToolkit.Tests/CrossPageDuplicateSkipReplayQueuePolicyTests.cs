using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageDuplicateSkipReplayQueuePolicyTests
{
    [Fact]
    public void Resolve_ShouldQueueVisualSyncReplay_WhenVisualSyncDuplicateSkipped()
    {
        var decision = new CrossPageDuplicateWindowDecision(
            ShouldSkip: true,
            Reason: CrossPageDuplicateWindowSkipReason.VisualSync);

        var replayDecision = CrossPageDuplicateSkipReplayQueuePolicy.Resolve(
            decision,
            CrossPageUpdateSourceKind.VisualSync,
            CrossPageUpdateSources.InkStateChanged);

        replayDecision.QueueVisualSyncReplay.Should().BeTrue();
        replayDecision.QueueInteractionReplay.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldQueueInteractionReplay_WhenInteractionDuplicateSkipped()
    {
        var decision = new CrossPageDuplicateWindowDecision(
            ShouldSkip: true,
            Reason: CrossPageDuplicateWindowSkipReason.Interaction);

        var replayDecision = CrossPageDuplicateSkipReplayQueuePolicy.Resolve(
            decision,
            CrossPageUpdateSourceKind.Interaction,
            CrossPageUpdateSources.PhotoPan);

        replayDecision.QueueVisualSyncReplay.Should().BeFalse();
        replayDecision.QueueInteractionReplay.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldNotQueue_WhenBackgroundDuplicateSkipped()
    {
        var decision = new CrossPageDuplicateWindowDecision(
            ShouldSkip: true,
            Reason: CrossPageDuplicateWindowSkipReason.BackgroundRefresh);

        var replayDecision = CrossPageDuplicateSkipReplayQueuePolicy.Resolve(
            decision,
            CrossPageUpdateSourceKind.BackgroundRefresh,
            CrossPageUpdateSources.NeighborRender);

        replayDecision.QueueVisualSyncReplay.Should().BeFalse();
        replayDecision.QueueInteractionReplay.Should().BeFalse();
    }
}
