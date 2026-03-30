using System.Collections.Generic;
using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class FloatingFrontSurfaceResolverTests
{
    private static readonly IWindowOrchestrator Orchestrator = new WindowOrchestrator();

    [Fact]
    public void Resolve_ShouldReturnPhotoFullscreen_WhenPhotoIsActive()
    {
        var stack = new List<ZOrderSurface> { ZOrderSurface.PhotoFullscreen };
        var snapshot = new FloatingWindowRuntimeSnapshot(
            OverlayVisible: true,
            OverlayActive: true,
            PhotoActive: true,
            PresentationFullscreen: false,
            WhiteboardActive: false,
            ImageManagerVisible: false,
            LauncherVisible: true);

        var surface = FloatingFrontSurfaceResolver.Resolve(Orchestrator, stack, snapshot);

        surface.Should().Be(ZOrderSurface.PhotoFullscreen);
    }

    [Fact]
    public void Resolve_ShouldReturnImageManager_WhenOnlyImageManagerVisible()
    {
        var stack = new List<ZOrderSurface>();
        var snapshot = new FloatingWindowRuntimeSnapshot(
            OverlayVisible: false,
            OverlayActive: false,
            PhotoActive: false,
            PresentationFullscreen: false,
            WhiteboardActive: false,
            ImageManagerVisible: true,
            LauncherVisible: false);

        var surface = FloatingFrontSurfaceResolver.Resolve(Orchestrator, stack, snapshot);

        surface.Should().Be(ZOrderSurface.ImageManager);
    }
}
