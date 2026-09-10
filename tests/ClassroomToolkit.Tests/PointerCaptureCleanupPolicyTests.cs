using AwesomeAssertions;
using ClassroomToolkit.App.Paint;

namespace ClassroomToolkit.Tests;

public sealed class PointerCaptureCleanupPolicyTests
{
    [Theory]
    [InlineData("mouse-capture-lost", false, true, true)]
    [InlineData("stylus-capture-lost", true, false, true)]
    [InlineData("mouse-capture-lost", false, false, false)]
    [InlineData("stylus-capture-lost", false, false, false)]
    [InlineData("overlay-deactivated", true, true, false)]
    [InlineData("overlay-closed", true, true, false)]
    public void ShouldDeferCleanup_ShouldWaitOnlyForPartialPointerCaptureLoss(
        string reason,
        bool mouseCaptured,
        bool stylusCaptured,
        bool expected)
    {
        PointerCaptureCleanupPolicy.ShouldDeferCleanup(reason, mouseCaptured, stylusCaptured)
            .Should().Be(expected);
    }
}
