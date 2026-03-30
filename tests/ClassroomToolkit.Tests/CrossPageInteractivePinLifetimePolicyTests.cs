using System;
using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageInteractivePinLifetimePolicyTests
{
    [Fact]
    public void ShouldReleasePin_ShouldReturnFalse_WhenHoldNotArmed()
    {
        var now = DateTime.UtcNow;
        CrossPageInteractivePinLifetimePolicy.ShouldReleasePin(
            holdUntilUtc: CrossPageRuntimeDefaults.UnsetTimestampUtc,
            nowUtc: now,
            interactionActive: false).Should().BeFalse();
    }

    [Fact]
    public void ShouldReleasePin_ShouldReturnFalse_WhenInteractionActive()
    {
        var now = DateTime.UtcNow;
        CrossPageInteractivePinLifetimePolicy.ShouldReleasePin(
            holdUntilUtc: now.AddMilliseconds(-50),
            nowUtc: now,
            interactionActive: true).Should().BeFalse();
    }

    [Fact]
    public void ShouldReleasePin_ShouldReturnTrue_WhenExpiredAndNoInteraction()
    {
        var now = DateTime.UtcNow;
        CrossPageInteractivePinLifetimePolicy.ShouldReleasePin(
            holdUntilUtc: now.AddMilliseconds(-1),
            nowUtc: now,
            interactionActive: false).Should().BeTrue();
    }
}
