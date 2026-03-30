using System;
using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class LauncherBubbleVisibleChangedApplyPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnVisibleChangedSuppressed_WhenSuppressed()
    {
        var decision = LauncherBubbleVisibleChangedApplyPolicy.Resolve(
            bubbleVisible: true,
            suppressVisibleChangedApply: true,
            suppressVisibleChangedUntilUtc: WindowDedupDefaults.UnsetTimestampUtc,
            nowUtc: DateTime.UtcNow);

        decision.ShouldApply.Should().BeFalse();
        decision.Reason.Should().Be(LauncherBubbleVisibleChangedApplyReason.VisibleChangedSuppressed);
    }

    [Fact]
    public void Resolve_ShouldReturnCooldownActive_WithinCooldownWindow()
    {
        var nowUtc = DateTime.UtcNow;

        var decision = LauncherBubbleVisibleChangedApplyPolicy.Resolve(
            bubbleVisible: true,
            suppressVisibleChangedApply: false,
            suppressVisibleChangedUntilUtc: nowUtc.AddMilliseconds(50),
            nowUtc: nowUtc);

        decision.ShouldApply.Should().BeFalse();
        decision.Reason.Should().Be(LauncherBubbleVisibleChangedApplyReason.CooldownActive);
    }

    [Fact]
    public void Resolve_ShouldReturnNone_WhenNotSuppressedAndCooldownElapsed()
    {
        var nowUtc = DateTime.UtcNow;

        var decision = LauncherBubbleVisibleChangedApplyPolicy.Resolve(
            bubbleVisible: true,
            suppressVisibleChangedApply: false,
            suppressVisibleChangedUntilUtc: nowUtc.AddMilliseconds(-1),
            nowUtc: nowUtc);

        decision.ShouldApply.Should().BeTrue();
        decision.Reason.Should().Be(LauncherBubbleVisibleChangedApplyReason.None);
    }

    [Fact]
    public void Resolve_ShouldReturnBubbleHidden_WhenBubbleNotVisible()
    {
        var decision = LauncherBubbleVisibleChangedApplyPolicy.Resolve(
            bubbleVisible: false,
            suppressVisibleChangedApply: false,
            suppressVisibleChangedUntilUtc: WindowDedupDefaults.UnsetTimestampUtc,
            nowUtc: DateTime.UtcNow);

        decision.ShouldApply.Should().BeFalse();
        decision.Reason.Should().Be(LauncherBubbleVisibleChangedApplyReason.BubbleHidden);
    }

    [Fact]
    public void ShouldApplyZOrder_ShouldMapResolveDecision()
    {
        var nowUtc = DateTime.UtcNow;

        LauncherBubbleVisibleChangedApplyPolicy.ShouldApplyZOrder(
            bubbleVisible: true,
            suppressVisibleChangedApply: false,
            suppressVisibleChangedUntilUtc: nowUtc.AddMilliseconds(-1),
            nowUtc: nowUtc).Should().BeTrue();
    }
}
