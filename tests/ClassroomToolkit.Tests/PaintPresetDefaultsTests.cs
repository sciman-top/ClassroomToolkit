using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class PaintPresetDefaultsTests
{
    [Fact]
    public void Defaults_ShouldMatchStabilizedValues()
    {
        PaintPresetDefaults.WpsDebounceDefaultMs.Should().Be(200);
        PaintPresetDefaults.PostInputRefreshDefaultMs.Should().Be(120);

        PaintPresetDefaults.WpsDebounceBalancedMs.Should().Be(120);
        PaintPresetDefaults.WpsDebounceResponsiveMs.Should().Be(80);
        PaintPresetDefaults.WpsDebounceStableMs.Should().Be(200);
        PaintPresetDefaults.WpsDebounceDualScreenMs.Should().Be(160);

        PaintPresetDefaults.PostInputBalancedMs.Should().Be(120);
        PaintPresetDefaults.PostInputResponsiveMs.Should().Be(80);
        PaintPresetDefaults.PostInputStableMs.Should().Be(140);
        PaintPresetDefaults.PostInputDualScreenMs.Should().Be(160);

        PaintPresetDefaults.WheelZoomBalanced.Should().Be(1.0008);
        PaintPresetDefaults.WheelZoomResponsive.Should().Be(1.0010);
        PaintPresetDefaults.WheelZoomStable.Should().Be(1.0006);
        PaintPresetDefaults.WheelZoomDualScreen.Should().Be(1.0007);

        PaintPresetDefaults.GestureSensitivityResponsive.Should().Be(1.2);
        PaintPresetDefaults.GestureSensitivityStable.Should().Be(0.8);
        PaintPresetDefaults.GestureSensitivityDualScreen.Should().Be(0.9);
    }
}
