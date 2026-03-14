using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageDisplayUpdateMinIntervalThresholdsTests
{
    [Fact]
    public void Thresholds_ShouldMatchStabilizedValues()
    {
        CrossPageDisplayUpdateMinIntervalThresholds.PanInkActiveMinMs.Should().Be(72);
        CrossPageDisplayUpdateMinIntervalThresholds.PanOnlyMinMs.Should().Be(60);
        CrossPageDisplayUpdateMinIntervalThresholds.InkOnlyMinMs.Should().Be(36);
    }
}
