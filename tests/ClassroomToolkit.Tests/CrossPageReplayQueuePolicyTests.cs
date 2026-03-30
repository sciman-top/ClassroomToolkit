using System;
using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageReplayQueuePolicyTests
{
    [Theory]
    [InlineData("VisualSync", true, false)]
    [InlineData("Interaction", false, true)]
    [InlineData("BackgroundRefresh", false, false)]
    public void Resolve_ShouldMapKindToReplayQueueFlags(
        string kindName,
        bool expectedVisualSync,
        bool expectedInteraction)
    {
        var kind = Enum.Parse<CrossPageUpdateSourceKind>(kindName);
        var decision = CrossPageReplayQueuePolicy.Resolve(kind);

        decision.QueueVisualSyncReplay.Should().Be(expectedVisualSync);
        decision.QueueInteractionReplay.Should().Be(expectedInteraction);
    }

    [Fact]
    public void Resolve_ShouldUpgradeVisualSyncToDualQueue_WhenImmediateSuffix()
    {
        var decision = CrossPageReplayQueuePolicy.Resolve(
            CrossPageUpdateSourceKind.VisualSync,
            CrossPageUpdateSources.WithImmediate(CrossPageUpdateSources.InkStateChanged));

        decision.QueueVisualSyncReplay.Should().BeTrue();
        decision.QueueInteractionReplay.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldNotUpgradeInteraction_WhenImmediateSuffix()
    {
        var decision = CrossPageReplayQueuePolicy.Resolve(
            CrossPageUpdateSourceKind.Interaction,
            CrossPageUpdateSources.WithImmediate(CrossPageUpdateSources.PointerUpFast));

        decision.QueueVisualSyncReplay.Should().BeFalse();
        decision.QueueInteractionReplay.Should().BeTrue();
    }
}
