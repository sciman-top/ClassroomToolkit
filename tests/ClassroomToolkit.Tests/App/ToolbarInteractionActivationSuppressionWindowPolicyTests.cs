using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ToolbarInteractionActivationSuppressionWindowPolicyTests
{
    [Fact]
    public void ResolveMs_ShouldReturnInteractive_WhenPhotoModeActive()
    {
        var ms = ToolbarInteractionActivationSuppressionWindowPolicy.ResolveMs(
            CreateSnapshot(photoModeActive: true, whiteboardActive: false),
            defaultMs: 90,
            interactiveMs: 130);

        ms.Should().Be(130);
    }

    [Fact]
    public void ResolveMs_ShouldReturnInteractive_WhenWhiteboardActive()
    {
        var ms = ToolbarInteractionActivationSuppressionWindowPolicy.ResolveMs(
            CreateSnapshot(photoModeActive: false, whiteboardActive: true),
            defaultMs: 90,
            interactiveMs: 130);

        ms.Should().Be(130);
    }

    [Fact]
    public void ResolveMs_ShouldReturnDefault_WhenNonInteractiveScene()
    {
        var ms = ToolbarInteractionActivationSuppressionWindowPolicy.ResolveMs(
            CreateSnapshot(photoModeActive: false, whiteboardActive: false),
            defaultMs: 90,
            interactiveMs: 130);

        ms.Should().Be(90);
    }

    private static ToolbarInteractionRetouchSnapshot CreateSnapshot(bool photoModeActive, bool whiteboardActive)
    {
        return new ToolbarInteractionRetouchSnapshot(
            OverlayVisible: true,
            PhotoModeActive: photoModeActive,
            WhiteboardActive: whiteboardActive,
            ToolbarVisible: true,
            ToolbarTopmost: true,
            RollCallVisible: false,
            RollCallTopmost: false,
            LauncherVisible: true,
            LauncherTopmost: true);
    }
}
