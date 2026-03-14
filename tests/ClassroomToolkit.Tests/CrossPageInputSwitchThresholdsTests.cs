using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageInputSwitchThresholdsTests
{
    [Fact]
    public void PointerHysteresis_ShouldMatchStabilizedValue()
    {
        CrossPageInputSwitchThresholds.PointerHysteresisDip.Should().Be(10.0);
    }
}
