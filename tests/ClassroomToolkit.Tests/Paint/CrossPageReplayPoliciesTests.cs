using AwesomeAssertions;
using ClassroomToolkit.App.Paint;
using System;
using Xunit;

namespace ClassroomToolkit.Tests.Paint;

public sealed class CrossPageDelayedDispatchFailureRecoveryPolicyTests
{
    [Fact]
    public void Resolve_ShouldDisableInline_WhenRecoveryAlreadyScheduled()
    {
        var decision = CrossPageReplayPolicies.ResolveCrossPageDelayedDispatchFailureRecovery(
            recoveryDispatchScheduled: true,
            dispatcherCheckAccess: true,
            dispatcherShutdownStarted: false,
            dispatcherShutdownFinished: false);

        decision.ShouldRecoverInline.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldDisableInline_WhenDispatcherIsShuttingDown()
    {
        var decision = CrossPageReplayPolicies.ResolveCrossPageDelayedDispatchFailureRecovery(
            recoveryDispatchScheduled: false,
            dispatcherCheckAccess: true,
            dispatcherShutdownStarted: true,
            dispatcherShutdownFinished: false);

        decision.ShouldRecoverInline.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldEnableInline_WhenUiThreadAvailableAndNotShuttingDown()
    {
        var decision = CrossPageReplayPolicies.ResolveCrossPageDelayedDispatchFailureRecovery(
            recoveryDispatchScheduled: false,
            dispatcherCheckAccess: true,
            dispatcherShutdownStarted: false,
            dispatcherShutdownFinished: false);

        decision.ShouldRecoverInline.Should().BeTrue();
    }
}

public sealed class CrossPageDuplicateSkipReplayQueuePolicyTests
{
    [Fact]
    public void Resolve_ShouldQueueVisualSyncReplay_WhenVisualSyncDuplicateSkipped()
    {
        var decision = new CrossPageDuplicateWindowDecision(
            ShouldSkip: true,
            Reason: CrossPageDuplicateWindowSkipReason.VisualSync);

        var replayDecision = CrossPageReplayPolicies.ResolveCrossPageDuplicateSkipReplayQueue(
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

        var replayDecision = CrossPageReplayPolicies.ResolveCrossPageDuplicateSkipReplayQueue(
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

        var replayDecision = CrossPageReplayPolicies.ResolveCrossPageDuplicateSkipReplayQueue(
            decision,
            CrossPageUpdateSourceKind.BackgroundRefresh,
            CrossPageUpdateSources.NeighborRender);

        replayDecision.QueueVisualSyncReplay.Should().BeFalse();
        replayDecision.QueueInteractionReplay.Should().BeFalse();
    }
}

public sealed class CrossPageImmediateDispatchPolicyTests
{
    [Fact]
    public void Resolve_ShouldUpgradeDelayedToDirect_ForImmediateSuffix()
    {
        var decision = new CrossPageDisplayUpdateDispatchDecision(
            Mode: CrossPageDisplayUpdateDispatchMode.Delayed,
            DelayMs: 12);

        var result = CrossPageReplayPolicies.ResolveCrossPageImmediateDispatch(
            decision,
            CrossPageUpdateDispatchSuffix.Immediate);

        result.Mode.Should().Be(CrossPageDisplayUpdateDispatchMode.Direct);
        result.DelayMs.Should().Be(0);
    }

    [Fact]
    public void Resolve_ShouldKeepDecision_ForNonImmediateSuffix()
    {
        var decision = new CrossPageDisplayUpdateDispatchDecision(
            Mode: CrossPageDisplayUpdateDispatchMode.Delayed,
            DelayMs: 12);

        var result = CrossPageReplayPolicies.ResolveCrossPageImmediateDispatch(
            decision,
            CrossPageUpdateDispatchSuffix.None);

        result.Should().Be(decision);
    }

    [Fact]
    public void Resolve_ShouldKeepNonDelayedMode_ForImmediateSuffix()
    {
        var decision = new CrossPageDisplayUpdateDispatchDecision(
            Mode: CrossPageDisplayUpdateDispatchMode.SkipPending,
            DelayMs: 0);

        var result = CrossPageReplayPolicies.ResolveCrossPageImmediateDispatch(
            decision,
            CrossPageUpdateDispatchSuffix.Immediate);

        result.Should().Be(decision);
    }
}

public sealed class CrossPageReplayDispatchFailurePolicyTests
{
    [Fact]
    public void Resolve_ShouldMapVisualSyncTarget()
    {
        var decision = CrossPageReplayPolicies.ResolveDispatchFailure(
            CrossPageReplayDispatchTarget.VisualSync);

        decision.QueueVisualSyncReplay.Should().BeTrue();
        decision.QueueInteractionReplay.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldMapInteractionTarget()
    {
        var decision = CrossPageReplayPolicies.ResolveDispatchFailure(
            CrossPageReplayDispatchTarget.Interaction);

        decision.QueueVisualSyncReplay.Should().BeFalse();
        decision.QueueInteractionReplay.Should().BeTrue();
    }
}

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
        CrossPageReplayPolicies.ResolveDispatch(visualSyncPending, interactionPending)
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
        var target = CrossPageReplayPolicies.ResolveDispatch(
            visualSyncReplayPending: true,
            interactionReplayPending: true,
            lastDispatchedTarget: lastTarget,
            preferInteractionReplay: preferInteraction);

        target.ToString().Should().Be(expectedTargetName);
    }
}

public sealed class CrossPageReplayDispatchRequestPolicyTests
{
    [Fact]
    public void ResolveSource_ShouldMapKnownTargets()
    {
        CrossPageReplayPolicies.ResolveSource(CrossPageReplayDispatchTarget.VisualSync)
            .Should().Be(CrossPageUpdateSources.InkVisualSyncReplay);
        CrossPageReplayPolicies.ResolveSource(CrossPageReplayDispatchTarget.Interaction)
            .Should().Be(CrossPageUpdateSources.InteractionReplay);
    }

    [Fact]
    public void ResolveSource_ShouldReturnNull_ForNone()
    {
        CrossPageReplayPolicies.ResolveSource(CrossPageReplayDispatchTarget.None)
            .Should().BeNull();
    }
}

public sealed class CrossPageReplayDispatchScheduleFallbackPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnNone_WhenDispatchScheduled()
    {
        var decision = CrossPageReplayPolicies.ResolveDispatchScheduleFallback(
            dispatchScheduled: true,
            dispatcherCheckAccess: true,
            dispatcherShutdownStarted: false,
            dispatcherShutdownFinished: false);

        decision.ShouldRunInline.Should().BeFalse();
        decision.ShouldRequeuePending.Should().BeFalse();
        decision.Reason.Should().Be(CrossPageReplayDispatchScheduleFallbackReason.None);
    }

    [Fact]
    public void Resolve_ShouldRunInline_WhenDispatchFailedOnUiThread()
    {
        var decision = CrossPageReplayPolicies.ResolveDispatchScheduleFallback(
            dispatchScheduled: false,
            dispatcherCheckAccess: true,
            dispatcherShutdownStarted: false,
            dispatcherShutdownFinished: false);

        decision.ShouldRunInline.Should().BeTrue();
        decision.ShouldRequeuePending.Should().BeFalse();
        decision.Reason.Should().Be(CrossPageReplayDispatchScheduleFallbackReason.InlineCurrentThread);
    }

    [Fact]
    public void Resolve_ShouldRequeue_WhenDispatchFailedOffUiThread()
    {
        var decision = CrossPageReplayPolicies.ResolveDispatchScheduleFallback(
            dispatchScheduled: false,
            dispatcherCheckAccess: false,
            dispatcherShutdownStarted: false,
            dispatcherShutdownFinished: false);

        decision.ShouldRunInline.Should().BeFalse();
        decision.ShouldRequeuePending.Should().BeTrue();
        decision.Reason.Should().Be(CrossPageReplayDispatchScheduleFallbackReason.RequeuePending);
    }
}

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
        var decision = CrossPageReplayPolicies.ResolveQueue(kind);

        decision.QueueVisualSyncReplay.Should().Be(expectedVisualSync);
        decision.QueueInteractionReplay.Should().Be(expectedInteraction);
    }

    [Fact]
    public void Resolve_ShouldUpgradeVisualSyncToDualQueue_WhenImmediateSuffix()
    {
        var decision = CrossPageReplayPolicies.ResolveQueue(
            CrossPageUpdateSourceKind.VisualSync,
            CrossPageUpdateSources.WithImmediate(CrossPageUpdateSources.InkStateChanged));

        decision.QueueVisualSyncReplay.Should().BeTrue();
        decision.QueueInteractionReplay.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldNotUpgradeInteraction_WhenImmediateSuffix()
    {
        var decision = CrossPageReplayPolicies.ResolveQueue(
            CrossPageUpdateSourceKind.Interaction,
            CrossPageUpdateSources.WithImmediate(CrossPageUpdateSources.PointerUpFast));

        decision.QueueVisualSyncReplay.Should().BeFalse();
        decision.QueueInteractionReplay.Should().BeTrue();
    }
}
