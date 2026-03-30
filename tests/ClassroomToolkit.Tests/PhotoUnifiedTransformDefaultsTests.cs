using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class PhotoUnifiedTransformDefaultsTests
{
    [Fact]
    public void Defaults_ShouldMatchStabilizedValues()
    {
        PhotoUnifiedTransformDefaults.DefaultTranslateDip.Should().Be(0.0);
    }
}
