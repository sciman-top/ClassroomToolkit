using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageDisplayUpdateMinIntervalThresholdsTests
{
    [Fact]
    public void Thresholds_ShouldMatchResponsiveValues()
    {
        CrossPageDisplayUpdateMinIntervalThresholds.PanInkActiveMinMs.Should().Be(24);
        CrossPageDisplayUpdateMinIntervalThresholds.PanOnlyMinMs.Should().Be(20);
        CrossPageDisplayUpdateMinIntervalThresholds.InkOnlyMinMs.Should().Be(16);
    }
}
