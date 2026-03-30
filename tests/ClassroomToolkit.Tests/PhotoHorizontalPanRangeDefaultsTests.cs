using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class PhotoHorizontalPanRangeDefaultsTests
{
    [Fact]
    public void Defaults_ShouldMatchStabilizedValues()
    {
        PhotoHorizontalPanRangeDefaults.MinSlackDip.Should().Be(24.0);
        PhotoHorizontalPanRangeDefaults.SlackRatio.Should().Be(0.06);
    }
}
