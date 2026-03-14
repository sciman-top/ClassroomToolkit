using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class PhotoTransformViewportDefaultsTests
{
    [Fact]
    public void Defaults_ShouldMatchStabilizedValues()
    {
        PhotoTransformViewportDefaults.MinUsableViewportDip.Should().Be(1.0);
        PhotoTransformViewportDefaults.DefaultScale.Should().Be(1.0);
        PhotoTransformViewportDefaults.MinScale.Should().Be(0.2);
        PhotoTransformViewportDefaults.MaxScale.Should().Be(4.0);
    }
}
