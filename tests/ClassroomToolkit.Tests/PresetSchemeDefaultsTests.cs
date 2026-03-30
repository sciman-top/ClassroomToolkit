using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class PresetSchemeDefaultsTests
{
    [Fact]
    public void Defaults_ShouldMatchStabilizedValues()
    {
        PresetSchemeDefaults.Custom.Should().Be("custom");
        PresetSchemeDefaults.Balanced.Should().Be("balanced");
        PresetSchemeDefaults.Responsive.Should().Be("responsive");
        PresetSchemeDefaults.Stable.Should().Be("stable");
        PresetSchemeDefaults.DualScreen.Should().Be("dual_screen");
    }
}
