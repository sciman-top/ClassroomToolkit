using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPagePostInputDelayThresholdsTests
{
    [Fact]
    public void Thresholds_ShouldMatchStabilizedValues()
    {
        CrossPagePostInputDelayThresholds.FallbackDelayMs.Should().Be(CrossPageRuntimeDefaults.PostInputRefreshDelayMs);
        CrossPagePostInputDelayThresholds.NeighborRenderMinMs.Should().Be(180);
        CrossPagePostInputDelayThresholds.NeighborMissingMinMs.Should().Be(200);
        CrossPagePostInputDelayThresholds.ReplayMinMs.Should().Be(220);
    }
}
