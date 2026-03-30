using System;
using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageReplayDispatchPolicyTests
{
    [Theory]
    [InlineData(true, true, "VisualSync")]
    [InlineData(true, false, "VisualSync")]
    [InlineData(false, true, "Interaction")]
    [InlineData(false, false, "None")]
    public void Resolve_ShouldSelectExpectedTarget(
        bool visualSyncPending,
        bool interactionPending,
        string expectedTarget)
    {
        CrossPageReplayDispatchPolicy.Resolve(visualSyncPending, interactionPending)
            .ToString()
            .Should()
            .Be(expectedTarget);
    }

    [Theory]
    [InlineData("VisualSync", false, "Interaction")]
    [InlineData("Interaction", false, "VisualSync")]
    [InlineData("None", false, "VisualSync")]
    [InlineData("None", true, "Interaction")]
    public void Resolve_WithLastDispatch_ShouldHonorPreferenceAndAlternateWhenBothPending(
        string lastTargetName,
        bool preferInteraction,
        string expectedTargetName)
    {
        var lastTarget = Enum.Parse<CrossPageReplayDispatchTarget>(lastTargetName);
        var target = CrossPageReplayDispatchPolicy.Resolve(
            visualSyncReplayPending: true,
            interactionReplayPending: true,
            lastDispatchedTarget: lastTarget,
            preferInteractionReplay: preferInteraction);

        target.ToString().Should().Be(expectedTargetName);
    }
}
