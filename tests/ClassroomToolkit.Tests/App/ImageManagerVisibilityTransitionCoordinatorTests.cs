using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ImageManagerVisibilityTransitionCoordinatorTests
{
    [Fact]
    public void ApplyOpen_ShouldRunOwnerShowNormalizeAndSurface_WhenPlanRequestsThem()
    {
        var ownerSyncCount = 0;
        var showCount = 0;
        var normalizeCount = 0;
        var surfaceCount = 0;
        var plan = new ImageManagerVisibilityTransitionPlan(
            SyncOwnersToOverlay: true,
            ShowWindow: true,
            NormalizeWindowState: true,
            DetachOwnerBeforeClose: false,
            CloseWindow: false,
            RequestZOrderApply: true,
            ForceEnforceZOrder: true,
            TouchImageManagerSurface: true);

        var result = ImageManagerVisibilityTransitionCoordinator.ApplyOpen(
            plan,
            () => ownerSyncCount++,
            () => showCount++,
            () => normalizeCount++,
            _ => surfaceCount++);

        result.AppliedOwnerSync.Should().BeTrue();
        result.ShowRequested.Should().BeTrue();
        result.NormalizeRequested.Should().BeTrue();
        result.AppliedSurfaceDecision.Should().BeTrue();
        ownerSyncCount.Should().Be(1);
        showCount.Should().Be(1);
        normalizeCount.Should().Be(1);
        surfaceCount.Should().Be(1);
    }

    [Fact]
    public void ApplyOpen_ShouldSkipSurface_WhenPlanDoesNotNeedSurfaceApply()
    {
        var surfaceCount = 0;
        var plan = new ImageManagerVisibilityTransitionPlan(
            SyncOwnersToOverlay: false,
            ShowWindow: false,
            NormalizeWindowState: false,
            DetachOwnerBeforeClose: false,
            CloseWindow: false,
            RequestZOrderApply: false,
            ForceEnforceZOrder: false,
            TouchImageManagerSurface: false);

        var result = ImageManagerVisibilityTransitionCoordinator.ApplyOpen(
            plan,
            () => { },
            () => { },
            () => { },
            _ => surfaceCount++);

        result.AppliedSurfaceDecision.Should().BeFalse();
        surfaceCount.Should().Be(0);
    }

    [Fact]
    public void ApplyCloseForPhotoSelection_ShouldDetachAndClose_WhenPlanRequestsBoth()
    {
        var detachCount = 0;
        var closeCount = 0;
        var plan = new ImageManagerVisibilityTransitionPlan(
            SyncOwnersToOverlay: false,
            ShowWindow: false,
            NormalizeWindowState: false,
            DetachOwnerBeforeClose: true,
            CloseWindow: true,
            RequestZOrderApply: false,
            ForceEnforceZOrder: false,
            TouchImageManagerSurface: false);

        var result = ImageManagerVisibilityTransitionCoordinator.ApplyCloseForPhotoSelection(
            plan,
            () => detachCount++,
            () => closeCount++);

        result.DetachedOwner.Should().BeTrue();
        result.CloseRequested.Should().BeTrue();
        detachCount.Should().Be(1);
        closeCount.Should().Be(1);
    }
}
