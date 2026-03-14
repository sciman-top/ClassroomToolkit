using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ZOrderRequestDedupIntervalPolicyTests
{
    [Fact]
    public void ResolveMs_ShouldUseWiderWindow_WhenPhotoModeActive()
    {
        var value = ZOrderRequestDedupIntervalPolicy.ResolveMs(
            overlayVisible: true,
            photoModeActive: true,
            whiteboardActive: false);

        value.Should().Be(ZOrderRequestDedupIntervalDefaults.InteractiveMs);
    }

    [Fact]
    public void ResolveMs_ShouldUseWiderWindow_WhenWhiteboardActive()
    {
        var value = ZOrderRequestDedupIntervalPolicy.ResolveMs(
            overlayVisible: true,
            photoModeActive: false,
            whiteboardActive: true);

        value.Should().Be(ZOrderRequestDedupIntervalDefaults.InteractiveMs);
    }

    [Fact]
    public void ResolveMs_ShouldUseDefault_WhenOverlayNotVisible()
    {
        var value = ZOrderRequestDedupIntervalPolicy.ResolveMs(
            overlayVisible: false,
            photoModeActive: true,
            whiteboardActive: true);

        value.Should().Be(ZOrderRequestBurstThresholds.RequestDedupMs);
    }
}
