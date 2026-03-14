using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageInteractionDuplicateWindowIntervalThresholdsTests
{
    [Fact]
    public void Thresholds_ShouldMatchStabilizedValues()
    {
        CrossPageInteractionDuplicateWindowIntervalThresholds.PhotoPanLikeMs.Should().Be(24);
        CrossPageInteractionDuplicateWindowIntervalThresholds.PointerUpFastMs.Should().Be(18);
    }
}
