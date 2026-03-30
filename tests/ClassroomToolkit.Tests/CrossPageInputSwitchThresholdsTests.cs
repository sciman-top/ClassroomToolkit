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

    [Fact]
    public void OutOfPageMoveSuppressMargin_ShouldMatchStabilizedValue()
    {
        CrossPageInputSwitchThresholds.OutOfPageMoveSuppressMarginDip.Should().Be(2.0);
    }

    [Fact]
    public void OutOfPageMoveSuppressPostSwitchGrace_ShouldMatchStabilizedValue()
    {
        CrossPageInputSwitchThresholds.OutOfPageMoveSuppressPostSwitchGraceMs.Should().Be(120);
    }
}
