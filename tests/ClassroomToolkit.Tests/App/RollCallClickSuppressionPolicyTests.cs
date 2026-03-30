using ClassroomToolkit.App;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class RollCallClickSuppressionPolicyTests
{
    [Fact]
    public void ExtendSuppressUntil_ShouldUseLaterTimestamp()
    {
        var nowUtc = new DateTime(2026, 3, 7, 2, 0, 0, DateTimeKind.Utc);
        var current = nowUtc.AddMilliseconds(300);

        var result = RollCallClickSuppressionPolicy.ExtendSuppressUntil(
            current,
            nowUtc,
            TimeSpan.FromMilliseconds(120));

        result.Should().Be(current);
    }

    [Fact]
    public void ExtendSuppressUntil_ShouldExtend_WhenDurationIsLonger()
    {
        var nowUtc = new DateTime(2026, 3, 7, 2, 0, 0, DateTimeKind.Utc);
        var current = nowUtc.AddMilliseconds(60);

        var result = RollCallClickSuppressionPolicy.ExtendSuppressUntil(
            current,
            nowUtc,
            TimeSpan.FromMilliseconds(240));

        result.Should().Be(nowUtc.AddMilliseconds(240));
    }

    [Fact]
    public void ShouldSuppress_ShouldReturnTrue_WhenWithinWindow()
    {
        var nowUtc = new DateTime(2026, 3, 7, 2, 0, 0, DateTimeKind.Utc);
        var suppressUntilUtc = nowUtc.AddMilliseconds(1);

        var suppressed = RollCallClickSuppressionPolicy.ShouldSuppress(suppressUntilUtc, nowUtc);

        suppressed.Should().BeTrue();
    }

    [Fact]
    public void ShouldSuppress_ShouldReturnFalse_WhenExpired()
    {
        var nowUtc = new DateTime(2026, 3, 7, 2, 0, 0, DateTimeKind.Utc);
        var suppressUntilUtc = nowUtc.AddMilliseconds(-1);

        var suppressed = RollCallClickSuppressionPolicy.ShouldSuppress(suppressUntilUtc, nowUtc);

        suppressed.Should().BeFalse();
    }

    [Fact]
    public void ExtendSuppressUntil_ShouldIgnoreNonPositiveDuration()
    {
        var nowUtc = new DateTime(2026, 3, 7, 2, 0, 0, DateTimeKind.Utc);
        var current = nowUtc.AddMilliseconds(100);

        var result = RollCallClickSuppressionPolicy.ExtendSuppressUntil(
            current,
            nowUtc,
            TimeSpan.Zero);

        result.Should().Be(current);
    }
}
