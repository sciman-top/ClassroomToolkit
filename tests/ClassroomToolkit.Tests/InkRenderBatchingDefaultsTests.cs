using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class InkRenderBatchingDefaultsTests
{
    [Fact]
    public void Defaults_ShouldMatchStabilizedValues()
    {
        InkRenderBatchingDefaults.ProximityPaddingPixels.Should().Be(24);
        InkRenderBatchingDefaults.AreaRatioThreshold.Should().Be(1.6);
    }
}
