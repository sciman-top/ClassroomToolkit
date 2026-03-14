using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class InkInputRuntimeDefaultsTests
{
    [Fact]
    public void Defaults_ShouldMatchStabilizedValues()
    {
        InkInputRuntimeDefaults.PredictionUpdateMinDtMs.Should().Be(0.5);
        InkInputRuntimeDefaults.RegionSelectionStrokeThicknessDip.Should().Be(2.0);
        InkInputRuntimeDefaults.RegionEraseMinSideDip.Should().Be(2.0);
        InkInputRuntimeDefaults.PhotoReferenceSizeMinDip.Should().Be(0.5);
    }
}
