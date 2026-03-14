using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageDisplayUpdateMinIntervalThresholdsTests
{
    [Fact]
    public void Thresholds_ShouldMatchStabilizedValues()
    {
        CrossPageDisplayUpdateMinIntervalThresholds.PanInkActiveMinMs.Should().Be(32);
        CrossPageDisplayUpdateMinIntervalThresholds.PanOnlyMinMs.Should().Be(28);
        CrossPageDisplayUpdateMinIntervalThresholds.InkOnlyMinMs.Should().Be(26);
    }
}
