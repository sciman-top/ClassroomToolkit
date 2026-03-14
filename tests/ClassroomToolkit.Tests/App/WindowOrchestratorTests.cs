using System.Collections.Generic;
using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class WindowOrchestratorTests
{
    [Fact]
    public void TouchSurface_ShouldMoveSurfaceToStackTail()
    {
        var orchestrator = new WindowOrchestrator();
        var stack = new List<ZOrderSurface>
        {
            ZOrderSurface.Whiteboard,
            ZOrderSurface.PhotoFullscreen
        };

        var changed = orchestrator.TouchSurface(stack, ZOrderSurface.Whiteboard);

        changed.Should().BeTrue();
        stack.Should().ContainInOrder(ZOrderSurface.PhotoFullscreen, ZOrderSurface.Whiteboard);
    }

    [Fact]
    public void TouchSurface_ShouldReturnFalse_WhenSurfaceAlreadyFront()
    {
        var orchestrator = new WindowOrchestrator();
        var stack = new List<ZOrderSurface>
        {
            ZOrderSurface.PhotoFullscreen,
            ZOrderSurface.Whiteboard
        };

        var changed = orchestrator.TouchSurface(stack, ZOrderSurface.Whiteboard);

        changed.Should().BeFalse();
        stack.Should().ContainInOrder(ZOrderSurface.PhotoFullscreen, ZOrderSurface.Whiteboard);
    }

    [Fact]
    public void ResolveFrontSurface_ShouldUseFallbackPriority_WhenStackHasNoActiveSurface()
    {
        var orchestrator = new WindowOrchestrator();
        var stack = new List<ZOrderSurface> { ZOrderSurface.Whiteboard };

        var front = orchestrator.ResolveFrontSurface(
            stack,
            photoActive: false,
            presentationFullscreen: false,
            whiteboardActive: false,
            imageManagerVisible: true);

        front.Should().Be(ZOrderSurface.ImageManager);
    }

    [Fact]
    public void PruneSurfaceStack_ShouldRemoveInactiveSurfaces()
    {
        var orchestrator = new WindowOrchestrator();
        var stack = new List<ZOrderSurface>
        {
            ZOrderSurface.PhotoFullscreen,
            ZOrderSurface.Whiteboard,
            ZOrderSurface.ImageManager
        };

        orchestrator.PruneSurfaceStack(
            stack,
            photoActive: false,
            presentationFullscreen: false,
            whiteboardActive: true,
            imageManagerVisible: false);

        stack.Should().ContainSingle().Which.Should().Be(ZOrderSurface.Whiteboard);
    }
}
