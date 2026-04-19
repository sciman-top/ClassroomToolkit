using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageViewportBoundsDefaultsTests
{
    [Fact]
    public void Defaults_ShouldMatchStabilizedValues()
    {
        CrossPageViewportBoundsDefaults.VisibilityMarginDip.Should().Be(16.0);
        CrossPageViewportBoundsDefaults.CenterRatio.Should().Be(0.5);
        CrossPageViewportBoundsDefaults.TranslateClampEpsilonDip.Should().Be(0.5);
        CrossPageViewportBoundsDefaults.ClampSlackMinDip.Should().Be(32.0);
        CrossPageViewportBoundsDefaults.ClampSlackViewportRatio.Should().Be(0.5);
    }
}
