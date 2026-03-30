using System.Collections.Generic;
using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public class FloatingWindowZOrderDecisionPolicyTests
{
    private static readonly IWindowOrchestrator Orchestrator = new WindowOrchestrator();

    [Fact]
    public void Resolve_ShouldPickImageManagerAsFrontSurface_WhenVisibleAndStackEmpty()
    {
        var surfaceStack = new List<ZOrderSurface>();
        var snapshot = new FloatingWindowRuntimeSnapshot(
            OverlayVisible: true,
            OverlayActive: false,
            PhotoActive: false,
            PresentationFullscreen: false,
            WhiteboardActive: false,
            ImageManagerVisible: true,
            LauncherVisible: true);

        var decision = FloatingWindowZOrderDecisionPolicy.Resolve(
            Orchestrator,
            surfaceStack,
            snapshot,
            FloatingTopmostVisibilitySnapshotPolicy.Resolve(
                toolbarVisible: true,
                rollCallVisible: false,
                launcherVisible: true,
                imageManagerVisible: true,
                overlayVisible: true),
            lastFrontSurface: null,
            lastPlan: null,
            forceEnforceZOrder: false);

        decision.FrontSurface.Should().Be(ZOrderSurface.ImageManager);
        decision.TopmostPlan.ImageManagerTopmost.Should().BeTrue();
        decision.EnforceZOrder.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldForceEnforce_WhenExplicitlyRequested()
    {
        var surfaceStack = new List<ZOrderSurface> { ZOrderSurface.PhotoFullscreen };
        var snapshot = new FloatingWindowRuntimeSnapshot(
            OverlayVisible: true,
            OverlayActive: true,
            PhotoActive: true,
            PresentationFullscreen: false,
            WhiteboardActive: false,
            ImageManagerVisible: false,
            LauncherVisible: true);
        var lastPlan = FloatingTopmostPlanPolicy.Resolve(
            ZOrderSurface.PhotoFullscreen,
            toolbarVisible: true,
            rollCallVisible: true,
            launcherVisible: true,
            imageManagerVisible: false,
            overlayVisible: true);

        var decision = FloatingWindowZOrderDecisionPolicy.Resolve(
            Orchestrator,
            surfaceStack,
            snapshot,
            FloatingTopmostVisibilitySnapshotPolicy.Resolve(
                toolbarVisible: true,
                rollCallVisible: true,
                launcherVisible: true,
                imageManagerVisible: false,
                overlayVisible: true),
            lastFrontSurface: ZOrderSurface.PhotoFullscreen,
            lastPlan: lastPlan,
            forceEnforceZOrder: true);

        decision.FrontSurface.Should().Be(ZOrderSurface.PhotoFullscreen);
        decision.EnforceZOrder.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldReuseExistingFrontSurface_WhenStillActive()
    {
        var surfaceStack = new List<ZOrderSurface>
        {
            ZOrderSurface.Whiteboard,
            ZOrderSurface.PhotoFullscreen
        };
        var snapshot = new FloatingWindowRuntimeSnapshot(
            OverlayVisible: true,
            OverlayActive: true,
            PhotoActive: true,
            PresentationFullscreen: false,
            WhiteboardActive: true,
            ImageManagerVisible: false,
            LauncherVisible: false);
        var lastPlan = FloatingTopmostPlanPolicy.Resolve(
            ZOrderSurface.PhotoFullscreen,
            toolbarVisible: true,
            rollCallVisible: false,
            launcherVisible: false,
            imageManagerVisible: false,
            overlayVisible: true);

        var decision = FloatingWindowZOrderDecisionPolicy.Resolve(
            Orchestrator,
            surfaceStack,
            snapshot,
            FloatingTopmostVisibilitySnapshotPolicy.Resolve(
                toolbarVisible: true,
                rollCallVisible: false,
                launcherVisible: false,
                imageManagerVisible: false,
                overlayVisible: true),
            lastFrontSurface: ZOrderSurface.PhotoFullscreen,
            lastPlan: lastPlan,
            forceEnforceZOrder: false);

        decision.FrontSurface.Should().Be(ZOrderSurface.PhotoFullscreen);
        decision.EnforceZOrder.Should().BeFalse();
    }
}
