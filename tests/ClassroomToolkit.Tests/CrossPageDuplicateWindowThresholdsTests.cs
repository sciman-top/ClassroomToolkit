using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageDuplicateWindowThresholdsTests
{
    [Fact]
    public void Thresholds_ShouldMatchStabilizedValues()
    {
        CrossPageDuplicateWindowThresholds.MinWindowMs.Should().Be(1);
        CrossPageDuplicateWindowThresholds.VisualSyncMs.Should().Be(12);
        CrossPageDuplicateWindowThresholds.BackgroundRefreshMs.Should().Be(24);
        CrossPageDuplicateWindowThresholds.InteractionMs.Should().Be(8);
    }
}
