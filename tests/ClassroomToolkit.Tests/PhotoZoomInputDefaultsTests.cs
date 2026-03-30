using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class PhotoZoomInputDefaultsTests
{
    [Fact]
    public void Defaults_ShouldMatchStabilizedValues()
    {
        PhotoZoomInputDefaults.WheelZoomBaseDefault.Should().Be(1.0008);
        PhotoZoomInputDefaults.WheelZoomBaseMin.Should().Be(1.0002);
        PhotoZoomInputDefaults.WheelZoomBaseMax.Should().Be(1.0020);
        PhotoZoomInputDefaults.GestureSensitivityDefault.Should().Be(1.0);
        PhotoZoomInputDefaults.GestureSensitivityMin.Should().Be(0.5);
        PhotoZoomInputDefaults.GestureSensitivityMax.Should().Be(1.8);
        PhotoZoomInputDefaults.GestureZoomNoiseThreshold.Should().Be(0.01);
        PhotoZoomInputDefaults.ZoomMinEventFactor.Should().Be(0.85);
        PhotoZoomInputDefaults.ZoomMaxEventFactor.Should().Be(1.18);
        PhotoZoomInputDefaults.ScaleApplyEpsilon.Should().Be(0.001);
        PhotoZoomInputDefaults.ManipulationTranslationEpsilonDip.Should().Be(0.01);
    }
}
