using System;
using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class LauncherBubbleZOrderApplyGatePolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnSpecificReason_WhenClosingOrBubbleMissing()
    {
        var nowUtc = DateTime.UtcNow;

        var appClosingDecision = LauncherBubbleZOrderApplyGatePolicy.Resolve(
            bubbleVisible: true,
            suppressVisibleChangedApply: false,
            suppressVisibleChangedUntilUtc: WindowDedupDefaults.UnsetTimestampUtc,
            nowUtc: nowUtc,
            appClosing: true,
            bubbleWindowExists: true);
        appClosingDecision.ShouldApply.Should().BeFalse();
        appClosingDecision.Reason.Should().Be(LauncherBubbleZOrderApplyGateReason.AppClosing);
        appClosingDecision.VisibleChangedReason.Should().Be(LauncherBubbleVisibleChangedApplyReason.None);

        var missingWindowDecision = LauncherBubbleZOrderApplyGatePolicy.Resolve(
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

        var suppressedDecision = LauncherBubbleZOrderApplyGatePolicy.Resolve(
            bubbleVisible: true,
            suppressVisibleChangedApply: true,
            suppressVisibleChangedUntilUtc: WindowDedupDefaults.UnsetTimestampUtc,
            nowUtc: nowUtc,
            appClosing: false,
            bubbleWindowExists: true);
        suppressedDecision.ShouldApply.Should().BeFalse();
        suppressedDecision.Reason.Should().Be(LauncherBubbleZOrderApplyGateReason.VisibleChangedSuppressed);
        suppressedDecision.VisibleChangedReason.Should().Be(LauncherBubbleVisibleChangedApplyReason.VisibleChangedSuppressed);

        var cooldownDecision = LauncherBubbleZOrderApplyGatePolicy.Resolve(
            bubbleVisible: true,
            suppressVisibleChangedApply: false,
            suppressVisibleChangedUntilUtc: nowUtc.AddMilliseconds(30),
            nowUtc: nowUtc,
            appClosing: false,
            bubbleWindowExists: true);
        cooldownDecision.ShouldApply.Should().BeFalse();
        cooldownDecision.Reason.Should().Be(LauncherBubbleZOrderApplyGateReason.CooldownActive);
        cooldownDecision.VisibleChangedReason.Should().Be(LauncherBubbleVisibleChangedApplyReason.CooldownActive);

        var applyDecision = LauncherBubbleZOrderApplyGatePolicy.Resolve(
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
        var decision = LauncherBubbleZOrderApplyGatePolicy.Resolve(
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

    [Fact]
    public void ShouldApply_ShouldMapResolveDecision()
    {
        LauncherBubbleZOrderApplyGatePolicy.ShouldApply(
            bubbleVisible: false,
            suppressVisibleChangedApply: false,
            suppressVisibleChangedUntilUtc: WindowDedupDefaults.UnsetTimestampUtc,
            nowUtc: DateTime.UtcNow,
            appClosing: false,
            bubbleWindowExists: true).Should().BeFalse();
    }
}
