using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class InkRenderingCacheDefaultsTests
{
    [Fact]
    public void Defaults_ShouldMatchStabilizedValues()
    {
        InkRenderingCacheDefaults.PenWidthMinMilli.Should().Be(1);
        InkRenderingCacheDefaults.PenWidthQuantizeScale.Should().Be(1000.0);
    }
}
