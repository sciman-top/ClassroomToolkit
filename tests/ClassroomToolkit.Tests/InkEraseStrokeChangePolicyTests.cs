using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class InkEraseStrokeChangePolicyTests
{
    [Fact]
    public void ShouldMarkStrokeChanged_ShouldReturnFalse_WhenNoGeometryChanged()
    {
        var changed = InkEraseStrokeChangePolicy.ShouldMarkStrokeChanged(
            geometryPathChanged: false,
            bloomGeometryChanged: false,
            ribbonGeometryChanged: false);

        changed.Should().BeFalse();
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    public void ShouldMarkStrokeChanged_ShouldReturnTrue_WhenAnyGeometryChanged(
        bool geometryPathChanged,
        bool bloomGeometryChanged,
        bool ribbonGeometryChanged)
    {
        var changed = InkEraseStrokeChangePolicy.ShouldMarkStrokeChanged(
            geometryPathChanged,
            bloomGeometryChanged,
            ribbonGeometryChanged);

        changed.Should().BeTrue();
    }
}
