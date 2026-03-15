using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageInputSwitchBounceGuardPolicyTests
{
    [Fact]
    public void ShouldSuppress_ShouldReturnTrue_WhenReverseSwitchHappensNearSeamWithinCooldown()
    {
        var nowUtc = new DateTime(2026, 3, 15, 10, 0, 0, DateTimeKind.Utc);
        var lastSwitchUtc = nowUtc.AddMilliseconds(-40);

        var suppress = CrossPageInputSwitchBounceGuardPolicy.ShouldSuppress(
            currentPage: 6,
            targetPage: 5,
            lastSwitchFromPage: 5,
            lastSwitchToPage: 6,
            lastSwitchUtc: lastSwitchUtc,
            nowUtc: nowUtc,
            pointerY: 504,
            seamY: 500,
            seamBandDip: 18,
            cooldownMs: 90);

        suppress.Should().BeTrue();
    }

    [Fact]
    public void ShouldSuppress_ShouldReturnFalse_WhenReverseSwitchIsFarFromSeam()
    {
        var nowUtc = new DateTime(2026, 3, 15, 10, 0, 0, DateTimeKind.Utc);
        var lastSwitchUtc = nowUtc.AddMilliseconds(-40);

        var suppress = CrossPageInputSwitchBounceGuardPolicy.ShouldSuppress(
            currentPage: 6,
            targetPage: 5,
            lastSwitchFromPage: 5,
            lastSwitchToPage: 6,
            lastSwitchUtc: lastSwitchUtc,
            nowUtc: nowUtc,
            pointerY: 545,
            seamY: 500,
            seamBandDip: 18,
            cooldownMs: 90);

        suppress.Should().BeFalse();
    }

    [Fact]
    public void ShouldSuppress_ShouldReturnFalse_WhenCooldownElapsed()
    {
        var nowUtc = new DateTime(2026, 3, 15, 10, 0, 0, DateTimeKind.Utc);
        var lastSwitchUtc = nowUtc.AddMilliseconds(-120);

        var suppress = CrossPageInputSwitchBounceGuardPolicy.ShouldSuppress(
            currentPage: 6,
            targetPage: 5,
            lastSwitchFromPage: 5,
            lastSwitchToPage: 6,
            lastSwitchUtc: lastSwitchUtc,
            nowUtc: nowUtc,
            pointerY: 504,
            seamY: 500,
            seamBandDip: 18,
            cooldownMs: 90);

        suppress.Should().BeFalse();
    }

    [Fact]
    public void ShouldSuppress_ShouldReturnFalse_WhenSwitchIsNotReverseDirection()
    {
        var nowUtc = new DateTime(2026, 3, 15, 10, 0, 0, DateTimeKind.Utc);
        var lastSwitchUtc = nowUtc.AddMilliseconds(-40);

        var suppress = CrossPageInputSwitchBounceGuardPolicy.ShouldSuppress(
            currentPage: 6,
            targetPage: 7,
            lastSwitchFromPage: 5,
            lastSwitchToPage: 6,
            lastSwitchUtc: lastSwitchUtc,
            nowUtc: nowUtc,
            pointerY: 504,
            seamY: 500,
            seamBandDip: 18,
            cooldownMs: 90);

        suppress.Should().BeFalse();
    }
}
