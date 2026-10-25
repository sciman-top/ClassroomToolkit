using AwesomeAssertions;
using ClassroomToolkit.App.Windowing;
using System;
using Xunit;
using ClassroomToolkit.App.Launcher;

namespace ClassroomToolkit.Tests.Windowing;

public sealed class LauncherBubbleVisibleChangedApplyPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnVisibleChangedSuppressed_WhenSuppressed()
    {
        var gate = LauncherBubblePolicies.ResolveZOrderApplyGate(
            bubbleVisible: true,
            suppressVisibleChangedApply: true,
            suppressVisibleChangedUntilUtc: WindowDedupDefaults.UnsetTimestampUtc,
            nowUtc: DateTime.UtcNow,
            appClosing: false,
            bubbleWindowExists: true);

        gate.ShouldApply.Should().BeFalse();
        gate.VisibleChangedReason.Should().Be(LauncherBubbleVisibleChangedApplyReason.VisibleChangedSuppressed);
    }

    [Fact]
    public void Resolve_ShouldReturnCooldownActive_WithinCooldownWindow()
    {
        var nowUtc = DateTime.UtcNow;

        var gate = LauncherBubblePolicies.ResolveZOrderApplyGate(
            bubbleVisible: true,
            suppressVisibleChangedApply: false,
            suppressVisibleChangedUntilUtc: nowUtc.AddMilliseconds(50),
            nowUtc: nowUtc,
            appClosing: false,
            bubbleWindowExists: true);

        gate.ShouldApply.Should().BeFalse();
        gate.VisibleChangedReason.Should().Be(LauncherBubbleVisibleChangedApplyReason.CooldownActive);
    }

    [Fact]
    public void Resolve_ShouldReturnNone_WhenNotSuppressedAndCooldownElapsed()
    {
        var nowUtc = DateTime.UtcNow;

        var gate = LauncherBubblePolicies.ResolveZOrderApplyGate(
            bubbleVisible: true,
            suppressVisibleChangedApply: false,
            suppressVisibleChangedUntilUtc: nowUtc.AddMilliseconds(-1),
            nowUtc: nowUtc,
            appClosing: false,
            bubbleWindowExists: true);

        gate.ShouldApply.Should().BeTrue();
        gate.VisibleChangedReason.Should().Be(LauncherBubbleVisibleChangedApplyReason.None);
    }

    [Fact]
    public void Resolve_ShouldReturnBubbleHidden_WhenBubbleNotVisible()
    {
        var gate = LauncherBubblePolicies.ResolveZOrderApplyGate(
            bubbleVisible: false,
            suppressVisibleChangedApply: false,
            suppressVisibleChangedUntilUtc: WindowDedupDefaults.UnsetTimestampUtc,
            nowUtc: DateTime.UtcNow,
            appClosing: false,
            bubbleWindowExists: true);

        gate.ShouldApply.Should().BeFalse();
        gate.VisibleChangedReason.Should().Be(LauncherBubbleVisibleChangedApplyReason.BubbleHidden);
    }

}

public sealed class LauncherBubbleVisibleChangedDedupIntervalPolicyTests
{
    [Fact]
    public void ResolveMs_ShouldReturnInteractiveWindow_WhenPhotoOrWhiteboardActive()
    {
        LauncherBubblePolicies.ResolveMs(
            overlayVisible: true,
            photoModeActive: true,
            whiteboardActive: false).Should().Be(130);

        LauncherBubblePolicies.ResolveMs(
            overlayVisible: true,
            photoModeActive: false,
            whiteboardActive: true).Should().Be(130);
    }

    [Fact]
    public void ResolveMs_ShouldReturnDefaultWindow_WhenOverlayNotInteractive()
    {
        LauncherBubblePolicies.ResolveMs(
            overlayVisible: false,
            photoModeActive: true,
            whiteboardActive: true).Should().Be(90);
    }
}

public sealed class LauncherBubbleVisibleChangedDedupPolicyTests
{
    [Fact]
    public void Resolve_ShouldApply_WhenNoHistory()
    {
        var now = DateTime.UtcNow;
        var decision = LauncherBubblePolicies.ResolveVisibleChangedDedup(
            currentVisibleState: true,
            lastVisibleState: null,
            lastEventUtc: WindowDedupDefaults.UnsetTimestampUtc,
            nowUtc: now);

        decision.ShouldApply.Should().BeTrue();
        decision.Reason.Should().Be(LauncherBubbleVisibleChangedDedupReason.NoHistory);
        decision.LastVisibleState.Should().BeTrue();
        decision.LastEventUtc.Should().Be(now);
    }

    [Fact]
    public void Resolve_ShouldSkip_WhenSameStateWithinWindow()
    {
        var now = DateTime.UtcNow;
        var last = now.AddMilliseconds(-40);
        var decision = LauncherBubblePolicies.ResolveVisibleChangedDedup(
            currentVisibleState: true,
            lastVisibleState: true,
            lastEventUtc: last,
            nowUtc: now,
            minIntervalMs: 90);

        decision.ShouldApply.Should().BeFalse();
        decision.Reason.Should().Be(LauncherBubbleVisibleChangedDedupReason.DuplicateWithinWindow);
        decision.LastVisibleState.Should().BeTrue();
        decision.LastEventUtc.Should().Be(last);
    }

    [Fact]
    public void Resolve_ShouldApply_WhenStateChangesWithinWindow()
    {
        var now = DateTime.UtcNow;
        var decision = LauncherBubblePolicies.ResolveVisibleChangedDedup(
            currentVisibleState: false,
            lastVisibleState: true,
            lastEventUtc: now.AddMilliseconds(-40),
            nowUtc: now,
            minIntervalMs: 90);

        decision.ShouldApply.Should().BeTrue();
        decision.Reason.Should().Be(LauncherBubbleVisibleChangedDedupReason.Applied);
        decision.LastVisibleState.Should().BeFalse();
        decision.LastEventUtc.Should().Be(now);
    }

    [Fact]
    public void Resolve_RuntimeStateOverload_ShouldRespectState()
    {
        var now = DateTime.UtcNow;
        var state = new LauncherBubbleVisibleChangedRuntimeState(
            LastVisibleState: true,
            LastEventUtc: now.AddMilliseconds(-30));

        var decision = LauncherBubblePolicies.ResolveVisibleChangedDedup(
            currentVisibleState: true,
            state,
            nowUtc: now,
            minIntervalMs: 90);

        decision.ShouldApply.Should().BeFalse();
        decision.Reason.Should().Be(LauncherBubbleVisibleChangedDedupReason.DuplicateWithinWindow);
        decision.LastVisibleState.Should().BeTrue();
    }
}

public sealed class LauncherBubbleVisibleChangedSuppressionPolicyTests
{
    [Fact]
    public void ResolveCooldownMs_ShouldReturnDefault_WhenNotInteractiveScene()
    {
        var cooldown = LauncherBubblePolicies.ResolveCooldownMs(
            overlayVisible: false,
            photoModeActive: true,
            whiteboardActive: true);

        cooldown.Should().Be(LauncherBubbleVisibleChangedSuppressionDefaults.TransitionCooldownMs);
    }

    [Fact]
    public void ResolveCooldownMs_ShouldReturnInteractive_WhenPhotoOrWhiteboardInOverlay()
    {
        var photoCooldown = LauncherBubblePolicies.ResolveCooldownMs(
            overlayVisible: true,
            photoModeActive: true,
            whiteboardActive: false);
        var boardCooldown = LauncherBubblePolicies.ResolveCooldownMs(
            overlayVisible: true,
            photoModeActive: false,
            whiteboardActive: true);

        photoCooldown.Should().Be(LauncherBubbleVisibleChangedSuppressionDefaults.InteractiveTransitionCooldownMs);
        boardCooldown.Should().Be(LauncherBubbleVisibleChangedSuppressionDefaults.InteractiveTransitionCooldownMs);
    }
}

public sealed class LauncherBubbleZOrderApplyGatePolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnSpecificReason_WhenClosingOrBubbleMissing()
    {
        var nowUtc = DateTime.UtcNow;

        var appClosingDecision = LauncherBubblePolicies.ResolveZOrderApplyGate(
            bubbleVisible: true,
            suppressVisibleChangedApply: false,
            suppressVisibleChangedUntilUtc: WindowDedupDefaults.UnsetTimestampUtc,
            nowUtc: nowUtc,
            appClosing: true,
            bubbleWindowExists: true);
        appClosingDecision.ShouldApply.Should().BeFalse();
        appClosingDecision.Reason.Should().Be(LauncherBubbleZOrderApplyGateReason.AppClosing);
        appClosingDecision.VisibleChangedReason.Should().Be(LauncherBubbleVisibleChangedApplyReason.None);

        var missingWindowDecision = LauncherBubblePolicies.ResolveZOrderApplyGate(
            bubbleVisible: true,
            suppressVisibleChangedApply: false,
            suppressVisibleChangedUntilUtc: WindowDedupDefaults.UnsetTimestampUtc,
            nowUtc: nowUtc,
            appClosing: false,
            bubbleWindowExists: false);
        missingWindowDecision.ShouldApply.Should().BeFalse();
        missingWindowDecision.Reason.Should().Be(LauncherBubbleZOrderApplyGateReason.BubbleWindowMissing);
        missingWindowDecision.VisibleChangedReason.Should().Be(LauncherBubbleVisibleChangedApplyReason.None);
    }

    [Fact]
    public void Resolve_ShouldRespectSuppressionAndCooldown()
    {
        var nowUtc = DateTime.UtcNow;

        var suppressedDecision = LauncherBubblePolicies.ResolveZOrderApplyGate(
            bubbleVisible: true,
            suppressVisibleChangedApply: true,
            suppressVisibleChangedUntilUtc: WindowDedupDefaults.UnsetTimestampUtc,
            nowUtc: nowUtc,
            appClosing: false,
            bubbleWindowExists: true);
        suppressedDecision.ShouldApply.Should().BeFalse();
        suppressedDecision.Reason.Should().Be(LauncherBubbleZOrderApplyGateReason.VisibleChangedSuppressed);
        suppressedDecision.VisibleChangedReason.Should().Be(LauncherBubbleVisibleChangedApplyReason.VisibleChangedSuppressed);

        var cooldownDecision = LauncherBubblePolicies.ResolveZOrderApplyGate(
            bubbleVisible: true,
            suppressVisibleChangedApply: false,
            suppressVisibleChangedUntilUtc: nowUtc.AddMilliseconds(30),
            nowUtc: nowUtc,
            appClosing: false,
            bubbleWindowExists: true);
        cooldownDecision.ShouldApply.Should().BeFalse();
        cooldownDecision.Reason.Should().Be(LauncherBubbleZOrderApplyGateReason.CooldownActive);
        cooldownDecision.VisibleChangedReason.Should().Be(LauncherBubbleVisibleChangedApplyReason.CooldownActive);

        var applyDecision = LauncherBubblePolicies.ResolveZOrderApplyGate(
            bubbleVisible: true,
            suppressVisibleChangedApply: false,
            suppressVisibleChangedUntilUtc: nowUtc.AddMilliseconds(-1),
            nowUtc: nowUtc,
            appClosing: false,
            bubbleWindowExists: true);
        applyDecision.ShouldApply.Should().BeTrue();
        applyDecision.Reason.Should().Be(LauncherBubbleZOrderApplyGateReason.None);
        applyDecision.VisibleChangedReason.Should().Be(LauncherBubbleVisibleChangedApplyReason.None);
    }

    [Fact]
    public void Resolve_ShouldReturnBubbleHidden_WhenBubbleHidden()
    {
        var decision = LauncherBubblePolicies.ResolveZOrderApplyGate(
            bubbleVisible: false,
            suppressVisibleChangedApply: false,
            suppressVisibleChangedUntilUtc: WindowDedupDefaults.UnsetTimestampUtc,
            nowUtc: DateTime.UtcNow,
            appClosing: false,
            bubbleWindowExists: true);

        decision.ShouldApply.Should().BeFalse();
        decision.Reason.Should().Be(LauncherBubbleZOrderApplyGateReason.BubbleHidden);
        decision.VisibleChangedReason.Should().Be(LauncherBubbleVisibleChangedApplyReason.BubbleHidden);
    }

}
