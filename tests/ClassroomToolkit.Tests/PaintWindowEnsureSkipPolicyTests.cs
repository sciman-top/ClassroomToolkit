using ClassroomToolkit.App.Windowing;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PaintWindowEnsureSkipPolicyTests
{
    [Fact]
    public void ShouldSkip_ShouldReturnTrue_WhenAllConditionsSatisfied()
    {
        var shouldSkip = PaintWindowEnsureSkipPolicy.ShouldSkip(
            hasOverlayWindow: true,
            hasToolbarWindow: true,
            eventsWired: true,
            shouldWireOverlayLifecycle: false,
            shouldWireToolbarLifecycle: false);

        shouldSkip.Should().BeTrue();
    }

    [Fact]
    public void ShouldSkip_ShouldReturnFalse_WhenAnyConditionNotSatisfied()
    {
        PaintWindowEnsureSkipPolicy.ShouldSkip(
            hasOverlayWindow: false,
            hasToolbarWindow: true,
            eventsWired: true,
            shouldWireOverlayLifecycle: false,
            shouldWireToolbarLifecycle: false).Should().BeFalse();

        PaintWindowEnsureSkipPolicy.ShouldSkip(
            hasOverlayWindow: true,
            hasToolbarWindow: true,
            eventsWired: false,
            shouldWireOverlayLifecycle: false,
            shouldWireToolbarLifecycle: false).Should().BeFalse();

        PaintWindowEnsureSkipPolicy.ShouldSkip(
            hasOverlayWindow: true,
            hasToolbarWindow: true,
            eventsWired: true,
            shouldWireOverlayLifecycle: true,
            shouldWireToolbarLifecycle: false).Should().BeFalse();
    }
}
