using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

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
