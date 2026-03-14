using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageInteractiveHoldDurationDefaultsTests
{
    [Fact]
    public void Defaults_ShouldMatchStabilizedValues()
    {
        CrossPageInteractiveHoldDurationDefaults.BaseMs.Should().Be(220);
        CrossPageInteractiveHoldDurationDefaults.ExtraPerNeighborMs.Should().Be(40);
        CrossPageInteractiveHoldDurationDefaults.MaxMs.Should().Be(380);
        CrossPageInteractiveHoldDurationDefaults.BrushModeExtraMs.Should().Be(80);
        CrossPageInteractiveHoldDurationDefaults.EraserModeExtraMs.Should().Be(40);
    }
}
