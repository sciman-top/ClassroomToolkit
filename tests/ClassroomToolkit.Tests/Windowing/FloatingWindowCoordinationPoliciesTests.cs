using AwesomeAssertions;
using ClassroomToolkit.App.Windowing;
using Xunit;

namespace ClassroomToolkit.Tests.Windowing;

public sealed class FloatingActivationGuardPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnNotBlocked_WhenNoUtilityWindowIsActive()
    {
        var decision = FloatingWindowCoordinationPolicies.ResolveFloatingActivationGuard(
            new FloatingUtilityActivitySnapshot(
                ToolbarActive: false,
                RollCallActive: false,
                ImageManagerActive: false,
                LauncherActive: false));

        decision.IsBlocked.Should().BeFalse();
        decision.Reason.Should().Be(FloatingActivationGuardReason.None);
    }

    [Theory]
    [InlineData(true, false, false, false, (int)FloatingActivationGuardReason.ToolbarActive)]
    [InlineData(false, true, false, false, (int)FloatingActivationGuardReason.RollCallActive)]
    [InlineData(false, false, true, false, (int)FloatingActivationGuardReason.ImageManagerActive)]
    [InlineData(false, false, false, true, (int)FloatingActivationGuardReason.LauncherActive)]
    public void Resolve_ShouldReturnBlockedReason_WhenAnyUtilityWindowIsActive(
        bool toolbarActive,
        bool rollCallActive,
        bool imageManagerActive,
        bool launcherActive,
        int expectedReason)
    {
        var decision = FloatingWindowCoordinationPolicies.ResolveFloatingActivationGuard(
            new FloatingUtilityActivitySnapshot(
                ToolbarActive: toolbarActive,
                RollCallActive: rollCallActive,
                ImageManagerActive: imageManagerActive,
                LauncherActive: launcherActive));

        decision.IsBlocked.Should().BeTrue();
        decision.Reason.Should().Be((FloatingActivationGuardReason)expectedReason);
    }

    [Fact]
    public void IsBlockedByUtilityWindows_ShouldMapResolveDecision()
    {
        FloatingWindowCoordinationPolicies.IsBlockedByUtilityWindows(
            toolbarActive: false,
            rollCallActive: false,
            imageManagerActive: true,
            launcherActive: false).Should().BeTrue();
    }
}

public sealed class FloatingDispatchQueuePolicyTests
{
    [Fact]
    public void RequestApply_ShouldQueueAndCaptureForceFlag()
    {
        var decision = FloatingWindowCoordinationPolicies.RequestApply(
            FloatingDispatchQueueState.Default,
            forceEnforceZOrder: true);

        decision.Action.Should().Be(FloatingDispatchQueueAction.QueueApply);
        decision.Reason.Should().Be(FloatingDispatchQueueReason.QueuedNewRequest);
        decision.State.ApplyQueued.Should().BeTrue();
        decision.State.ForceEnforceZOrder.Should().BeTrue();
    }

    [Fact]
    public void RequestApply_ShouldMergeForceFlag_WhenAlreadyQueued()
    {
        var state = new FloatingDispatchQueueState(
            ApplyQueued: true,
            ForceEnforceZOrder: false);

        var decision = FloatingWindowCoordinationPolicies.RequestApply(
            state,
            forceEnforceZOrder: true);

        decision.Action.Should().Be(FloatingDispatchQueueAction.None);
        decision.Reason.Should().Be(FloatingDispatchQueueReason.MergedIntoQueuedRequest);
        decision.State.ApplyQueued.Should().BeTrue();
        decision.State.ForceEnforceZOrder.Should().BeTrue();
    }

    [Fact]
    public void OnApplyExecuted_ShouldClearQueuedAndForceFlags()
    {
        var next = FloatingWindowCoordinationPolicies.OnApplyExecuted(
            new FloatingDispatchQueueState(
                ApplyQueued: true,
                ForceEnforceZOrder: true));

        next.ApplyQueued.Should().BeFalse();
        next.ForceEnforceZOrder.Should().BeFalse();
    }
}

public sealed class FloatingWindowActivationPolicyTests
{
    [Fact]
    public void Resolve_ShouldActivateOverlay_WhenOverlayNeedsActivation_AndNoUtilityWindowIsActive()
    {
        var plan = FloatingWindowCoordinationPolicies.ResolveFloatingWindowActivation(
            new FloatingWindowActivationSnapshot(
                OverlayVisible: true,
                OverlayShouldActivate: true,
                OverlayActive: false,
                ImageManagerTopmost: false,
                ImageManagerActive: false,
                UtilityActivity: new FloatingUtilityActivitySnapshot(
                    ToolbarActive: false,
                    RollCallActive: false,
                    ImageManagerActive: false,
                    LauncherActive: false)));

        plan.ActivateOverlay.Should().BeTrue();
        plan.ActivateImageManager.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldActivateImageManager_WhenImageManagerIsFront_AndNoUtilityWindowIsActive()
    {
        var plan = FloatingWindowCoordinationPolicies.ResolveFloatingWindowActivation(
            new FloatingWindowActivationSnapshot(
                OverlayVisible: true,
                OverlayShouldActivate: false,
                OverlayActive: false,
                ImageManagerTopmost: true,
                ImageManagerActive: false,
                UtilityActivity: new FloatingUtilityActivitySnapshot(
                    ToolbarActive: false,
                    RollCallActive: false,
                    ImageManagerActive: false,
                    LauncherActive: false)));

        plan.ActivateOverlay.Should().BeFalse();
        plan.ActivateImageManager.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldBlockAllActivation_WhenUtilityWindowIsActive()
    {
        var plan = FloatingWindowCoordinationPolicies.ResolveFloatingWindowActivation(
            new FloatingWindowActivationSnapshot(
                OverlayVisible: true,
                OverlayShouldActivate: true,
                OverlayActive: false,
                ImageManagerTopmost: true,
                ImageManagerActive: false,
                UtilityActivity: new FloatingUtilityActivitySnapshot(
                    ToolbarActive: true,
                    RollCallActive: false,
                    ImageManagerActive: false,
                    LauncherActive: false)));

        plan.ActivateOverlay.Should().BeFalse();
        plan.ActivateImageManager.Should().BeFalse();
    }
}

public class FloatingWindowRuntimeSnapshotPolicyTests
{
    [Fact]
    public void Resolve_ShouldTreatMinimizedImageManagerAsNotVisible()
    {
        var snapshot = FloatingWindowCoordinationPolicies.ResolveFloatingWindowRuntimeSnapshot(
            overlayVisible: true,
            overlayActive: false,
            photoActive: true,
            presentationFullscreen: false,
            whiteboardActive: false,
            imageManagerVisible: true,
            imageManagerMinimized: true,
            launcherVisible: true);

        snapshot.ImageManagerVisible.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldKeepImageManagerVisibleWhenNotMinimized()
    {
        var snapshot = FloatingWindowCoordinationPolicies.ResolveFloatingWindowRuntimeSnapshot(
            overlayVisible: true,
            overlayActive: false,
            photoActive: false,
            presentationFullscreen: true,
            whiteboardActive: false,
            imageManagerVisible: true,
            imageManagerMinimized: false,
            launcherVisible: false);

        snapshot.ImageManagerVisible.Should().BeTrue();
        snapshot.PresentationFullscreen.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldPreserveOverlayAndLauncherFlags()
    {
        var snapshot = FloatingWindowCoordinationPolicies.ResolveFloatingWindowRuntimeSnapshot(
            overlayVisible: true,
            overlayActive: true,
            photoActive: false,
            presentationFullscreen: false,
            whiteboardActive: true,
            imageManagerVisible: false,
            imageManagerMinimized: false,
            launcherVisible: true);

        snapshot.OverlayVisible.Should().BeTrue();
        snapshot.OverlayActive.Should().BeTrue();
        snapshot.WhiteboardActive.Should().BeTrue();
        snapshot.LauncherVisible.Should().BeTrue();
    }
}
