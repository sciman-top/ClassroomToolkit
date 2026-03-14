using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class StylusRuntimeDefaultsTests
{
    [Fact]
    public void Defaults_ShouldMatchStabilizedValues()
    {
        StylusRuntimeDefaults.PressureGammaStable.Should().Be(1.16);
        StylusRuntimeDefaults.PressureGammaResponsive.Should().Be(0.88);
        StylusRuntimeDefaults.PressureGammaDefault.Should().Be(1.0);
        StylusRuntimeDefaults.CalibratedRangeSeedMinWidth.Should().Be(0.01);
        StylusRuntimeDefaults.CalibratedLowDefault.Should().Be(0.0);
        StylusRuntimeDefaults.CalibratedHighDefault.Should().Be(1.0);
    }
}
