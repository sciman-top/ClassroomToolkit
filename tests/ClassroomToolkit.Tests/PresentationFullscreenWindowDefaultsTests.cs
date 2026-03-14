using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class PresentationFullscreenWindowDefaultsTests
{
    [Fact]
    public void Defaults_ShouldMatchStabilizedValues()
    {
        PresentationFullscreenWindowDefaults.BoundsTolerancePixels.Should().Be(2);
    }
}
