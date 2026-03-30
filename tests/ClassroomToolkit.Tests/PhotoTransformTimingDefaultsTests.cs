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
        PhotoTransformTimingDefaults.TransformSaveDebounceMs.Should().Be(120);
        PhotoTransformTimingDefaults.UnifiedTransformBroadcastDebounceMs.Should().Be(300);
    }
}
