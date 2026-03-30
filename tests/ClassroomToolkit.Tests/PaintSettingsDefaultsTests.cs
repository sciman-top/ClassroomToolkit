using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class PaintSettingsDefaultsTests
{
    [Fact]
    public void Defaults_ShouldMatchStabilizedValues()
    {
        PaintSettingsDefaults.DoubleComparisonEpsilon.Should().Be(0.0001);
        PaintSettingsDefaults.ComboTagComparisonEpsilon.Should().Be(0.001);
        PaintSettingsDefaults.PercentMin.Should().Be(0.0);
        PaintSettingsDefaults.PercentMax.Should().Be(100.0);
        PaintSettingsDefaults.PercentToByteScale.Should().Be(255.0);
    }
}
