using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class OverlayInputPassthroughDefaultsTests
{
    [Fact]
    public void Defaults_ShouldMatchStabilizedValues()
    {
        OverlayInputPassthroughDefaults.OpacityEpsilon.Should().Be(0.001);
    }
}
