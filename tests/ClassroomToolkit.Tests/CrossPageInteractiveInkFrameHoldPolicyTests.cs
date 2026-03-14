using System;
using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageInteractiveInkFrameHoldPolicyTests
{
    [Fact]
    public void ShouldHoldReplacement_ShouldReturnFalse_WhenNoCurrentFrame()
    {
        var now = DateTime.UtcNow;
        var hold = CrossPageInteractiveInkFrameHoldPolicy.ShouldHoldReplacement(
            pageIndex: 2,
            pinnedNeighborPage: 2,
            holdUntilUtc: now.AddMilliseconds(100),
            nowUtc: now,
            hasCurrentInkFrame: false);

        hold.Should().BeFalse();
    }

    [Fact]
    public void ShouldHoldReplacement_ShouldReturnTrue_WhenPinnedAndWithinWindow()
    {
        var now = DateTime.UtcNow;
        var hold = CrossPageInteractiveInkFrameHoldPolicy.ShouldHoldReplacement(
            pageIndex: 2,
            pinnedNeighborPage: 2,
            holdUntilUtc: now.AddMilliseconds(80),
            nowUtc: now,
            hasCurrentInkFrame: true);

        hold.Should().BeTrue();
    }

    [Fact]
    public void ShouldHoldReplacement_ShouldReturnFalse_WhenExpired()
    {
        var now = DateTime.UtcNow;
        var hold = CrossPageInteractiveInkFrameHoldPolicy.ShouldHoldReplacement(
            pageIndex: 2,
            pinnedNeighborPage: 2,
            holdUntilUtc: now.AddMilliseconds(-1),
            nowUtc: now,
            hasCurrentInkFrame: true);

        hold.Should().BeFalse();
    }

    [Fact]
    public void ShouldHoldReplacement_ShouldReturnFalse_WhenPageNotPinned()
    {
        var now = DateTime.UtcNow;
        var hold = CrossPageInteractiveInkFrameHoldPolicy.ShouldHoldReplacement(
            pageIndex: 3,
            pinnedNeighborPage: 2,
            holdUntilUtc: now.AddMilliseconds(80),
            nowUtc: now,
            hasCurrentInkFrame: true);

        hold.Should().BeFalse();
    }

    [Fact]
    public void ShouldHoldReplacement_ShouldReturnFalse_WhenHoldTimestampNotInitialized()
    {
        var now = DateTime.UtcNow;
        var hold = CrossPageInteractiveInkFrameHoldPolicy.ShouldHoldReplacement(
            pageIndex: 2,
            pinnedNeighborPage: 2,
            holdUntilUtc: CrossPageRuntimeDefaults.UnsetTimestampUtc,
            nowUtc: now,
            hasCurrentInkFrame: true);

        hold.Should().BeFalse();
    }
}
