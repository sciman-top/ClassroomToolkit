using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageReplayDispatchFailurePolicyTests
{
    [Fact]
    public void Resolve_ShouldMapVisualSyncTarget()
    {
        var decision = CrossPageReplayDispatchFailurePolicy.Resolve(
            CrossPageReplayDispatchTarget.VisualSync);

        decision.QueueVisualSyncReplay.Should().BeTrue();
        decision.QueueInteractionReplay.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldMapInteractionTarget()
    {
        var decision = CrossPageReplayDispatchFailurePolicy.Resolve(
            CrossPageReplayDispatchTarget.Interaction);

        decision.QueueVisualSyncReplay.Should().BeFalse();
        decision.QueueInteractionReplay.Should().BeTrue();
    }
}
