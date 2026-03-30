using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageBrushContinuationDefaultsTests
{
    [Fact]
    public void Defaults_ShouldMatchStabilizedValues()
    {
        CrossPageBrushContinuationDefaults.InPageOffsetDipMin.Should().Be(0.08);
        CrossPageBrushContinuationDefaults.InPageOffsetDipMax.Should().Be(0.22);
        CrossPageBrushContinuationDefaults.InPageOffsetFactor.Should().Be(0.01);
        CrossPageBrushContinuationDefaults.ReplayDistanceToleranceDip.Should().Be(0.35);
    }
}
