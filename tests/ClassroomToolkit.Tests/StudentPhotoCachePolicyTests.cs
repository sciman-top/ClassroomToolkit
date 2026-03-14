using ClassroomToolkit.App.Photos;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class StudentPhotoCachePolicyTests
{
    [Fact]
    public void ShouldReuseCache_ShouldReturnTrue_WhenWithinTtl()
    {
        var nowUtc = new DateTime(2026, 3, 7, 4, 0, 0, DateTimeKind.Utc);
        var reuse = StudentPhotoCachePolicy.ShouldReuseCache(
            nowUtc,
            nowUtc.AddMinutes(-5),
            TimeSpan.FromMinutes(10));

        reuse.Should().BeTrue();
    }

    [Fact]
    public void ShouldReuseCache_ShouldReturnFalse_WhenTtlExpired()
    {
        var nowUtc = new DateTime(2026, 3, 7, 4, 0, 0, DateTimeKind.Utc);
        var reuse = StudentPhotoCachePolicy.ShouldReuseCache(
            nowUtc,
            nowUtc.AddMinutes(-11),
            TimeSpan.FromMinutes(10));

        reuse.Should().BeFalse();
    }
}
