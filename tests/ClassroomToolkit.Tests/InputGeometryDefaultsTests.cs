using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class InputGeometryDefaultsTests
{
    [Fact]
    public void Defaults_ShouldMatchStabilizedValues()
    {
        InputGeometryDefaults.MinRenderableImageSideDip.Should().Be(0.5);
    }
}
