using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PresentationNavigationDebounceMsPolicyTests
{
    [Theory]
    [InlineData(0, 200, 0)]
    [InlineData(120, 200, 120)]
    [InlineData(-1, 200, 200)]
    [InlineData(-1, -1, 0)]
    public void Resolve_ShouldPreferConfiguredAndClampFallback(
        int configuredMs,
        int fallbackMs,
        int expected)
    {
        var actual = PresentationNavigationDebounceMsPolicy.Resolve(configuredMs, fallbackMs);

        actual.Should().Be(expected);
    }
}
