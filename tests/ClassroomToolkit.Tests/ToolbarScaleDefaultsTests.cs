using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class ToolbarScaleDefaultsTests
{
    [Fact]
    public void Defaults_ShouldMatchStabilizedValues()
    {
        ToolbarScaleDefaults.Min.Should().Be(0.8);
        ToolbarScaleDefaults.Default.Should().Be(1.0);
        ToolbarScaleDefaults.Max.Should().Be(2.0);
    }
}
