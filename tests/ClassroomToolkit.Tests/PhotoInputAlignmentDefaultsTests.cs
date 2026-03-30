using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class PhotoInputAlignmentDefaultsTests
{
    [Fact]
    public void Defaults_ShouldMatchStabilizedValues()
    {
        PhotoInputAlignmentDefaults.GestureSensitivityMin.Should().Be(0.2);
        PhotoInputAlignmentDefaults.GestureSensitivityMax.Should().Be(3.0);
        PhotoInputAlignmentDefaults.MinEventFactorFloor.Should().Be(0.01);
        PhotoInputAlignmentDefaults.IgnoreFactorDelta.Should().Be(0.001);
        PhotoInputAlignmentDefaults.PanResistanceFactorDefault.Should().Be(0.42);
        PhotoInputAlignmentDefaults.PanResistanceFactorMin.Should().Be(0.05);
        PhotoInputAlignmentDefaults.PanResistanceFactorMax.Should().Be(0.95);
    }
}
