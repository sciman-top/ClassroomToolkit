using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class PhotoTransformTimingDefaultsTests
{
    [Fact]
    public void TimingDefaults_ShouldMatchStabilizedValues()
    {
        PhotoTransformTimingDefaults.WheelSuppressAfterGestureMs.Should().Be(180);
        PhotoTransformTimingDefaults.SmoothZoomResponseMs.Should().Be(78.0);
        PhotoTransformTimingDefaults.SmoothZoomFrameEpsilon.Should().Be(0.0005);
        PhotoTransformTimingDefaults.TransformSaveDebounceMs.Should().Be(120);
        PhotoTransformTimingDefaults.UnifiedTransformBroadcastDebounceMs.Should().Be(300);
    }
}
