using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageReplayQueueDecisionFactoryTests
{
    [Fact]
    public void VisualSync_ShouldQueueOnlyVisualSyncReplay()
    {
        var decision = CrossPageReplayQueueDecisionFactory.VisualSync();

        decision.QueueVisualSyncReplay.Should().BeTrue();
        decision.QueueInteractionReplay.Should().BeFalse();
    }

    [Fact]
    public void Interaction_ShouldQueueOnlyInteractionReplay()
    {
        var decision = CrossPageReplayQueueDecisionFactory.Interaction();

        decision.QueueVisualSyncReplay.Should().BeFalse();
        decision.QueueInteractionReplay.Should().BeTrue();
    }
}
