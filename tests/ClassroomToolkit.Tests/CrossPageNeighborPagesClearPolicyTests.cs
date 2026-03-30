using System;
using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageNeighborPagesClearPolicyTests
{
    [Fact]
    public void ShouldKeepFrames_ShouldReturnFalse_WhenNoVisibleFrame()
    {
        var result = CrossPageNeighborPagesClearPolicy.ShouldKeepFrames(
            hasVisibleNeighborFrame: false,
            interactionActive: true,
            lastNonEmptyUtc: DateTime.UtcNow,
            nowUtc: DateTime.UtcNow,
            clearGraceMs: 180);

        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldKeepFrames_ShouldReturnTrue_WhenInteractionActive()
    {
        var now = DateTime.UtcNow;
        var result = CrossPageNeighborPagesClearPolicy.ShouldKeepFrames(
            hasVisibleNeighborFrame: true,
            interactionActive: true,
            lastNonEmptyUtc: now.AddMilliseconds(-1000),
            nowUtc: now,
            clearGraceMs: 180);

        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldKeepFrames_ShouldRespectGraceWindow_WhenIdle()
    {
        var now = DateTime.UtcNow;
        var keep = CrossPageNeighborPagesClearPolicy.ShouldKeepFrames(
            hasVisibleNeighborFrame: true,
            interactionActive: false,
            lastNonEmptyUtc: now.AddMilliseconds(-80),
            nowUtc: now,
            clearGraceMs: 180);
        var clear = CrossPageNeighborPagesClearPolicy.ShouldKeepFrames(
            hasVisibleNeighborFrame: true,
            interactionActive: false,
            lastNonEmptyUtc: now.AddMilliseconds(-400),
            nowUtc: now,
            clearGraceMs: 180);

        keep.Should().BeTrue();
        clear.Should().BeFalse();
    }
}
