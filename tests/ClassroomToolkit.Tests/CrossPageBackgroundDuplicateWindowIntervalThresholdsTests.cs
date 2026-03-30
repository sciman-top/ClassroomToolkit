using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageBackgroundDuplicateWindowIntervalThresholdsTests
{
    [Fact]
    public void Thresholds_ShouldMatchStabilizedValues()
    {
        CrossPageBackgroundDuplicateWindowIntervalThresholds.NeighborMissingMs.Should().Be(36);
        CrossPageBackgroundDuplicateWindowIntervalThresholds.NeighborSidecarMs.Should().Be(32);
        CrossPageBackgroundDuplicateWindowIntervalThresholds.NeighborRenderMs.Should().Be(28);
    }
}
