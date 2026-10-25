using AwesomeAssertions;
using ClassroomToolkit.App.Windowing;
using System.Windows;
using Xunit;

namespace ClassroomToolkit.Tests.Windowing;

public sealed class PaintVisibilityTransitionPolicyTests
{
    [Fact]
    public void ResolveEnsureOverlayVisible_AlwaysRequestsEnsureVisible()
    {
        PaintWindowVisibilityPolicies.ResolveEnsureOverlayVisible(overlayVisible: false)
            .Should()
            .Be(PaintVisibilityTransition.EnsureOverlayVisible);
        PaintWindowVisibilityPolicies.ResolveEnsureOverlayVisible(overlayVisible: true)
            .Should()
            .Be(PaintVisibilityTransition.EnsureOverlayVisible);
    }

    [Fact]
    public void ResolvePaintToggle_MapsOverlayStateToHideOrShow()
    {
        PaintWindowVisibilityPolicies.ResolvePaintToggle(overlayVisible: true)
            .Should()
            .Be(PaintVisibilityTransition.ToggleHidePaint);
        PaintWindowVisibilityPolicies.ResolvePaintToggle(overlayVisible: false)
            .Should()
            .Be(PaintVisibilityTransition.ToggleShowPaint);
    }

    [Fact]
    public void ResolvePhotoModeChange_MapsActiveStateToEnterOrExit()
    {
        PaintWindowVisibilityPolicies.ResolvePhotoModeChange(
                photoModeActive: true,
                toolbarWindowState: WindowState.Minimized)
            .Should()
            .Be(PaintVisibilityTransition.EnterPhotoMode);
        PaintWindowVisibilityPolicies.ResolvePhotoModeChange(
                photoModeActive: false,
                toolbarWindowState: WindowState.Normal)
            .Should()
            .Be(PaintVisibilityTransition.ExitPhotoMode);
    }

    [Fact]
    public void ShouldNormalizeToolbarWindowState_OnlyForMinimizedEnterPhoto()
    {
        PaintWindowVisibilityPolicies
            .ShouldNormalizeToolbarWindowState(
                PaintVisibilityTransition.EnterPhotoMode,
                WindowState.Minimized)
            .Should()
            .BeTrue();
        PaintWindowVisibilityPolicies
            .ShouldNormalizeToolbarWindowState(
                PaintVisibilityTransition.EnterPhotoMode,
                WindowState.Normal)
            .Should()
            .BeFalse();
        PaintWindowVisibilityPolicies
            .ShouldNormalizeToolbarWindowState(
                PaintVisibilityTransition.ExitPhotoMode,
                WindowState.Minimized)
            .Should()
            .BeFalse();
    }
}

public sealed class PaintWindowCreationPolicyTests
{
    [Theory]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void ShouldEnsureWindows_ShouldMatchExpected(
        bool hasOverlayWindow,
        bool hasToolbarWindow,
        bool expected)
    {
        PaintWindowVisibilityPolicies.ShouldEnsureWindows(hasOverlayWindow, hasToolbarWindow)
            .Should()
            .Be(expected);
    }
}

public sealed class PaintWindowEnsureSkipPolicyTests
{
    [Fact]
    public void ShouldSkip_ShouldReturnTrue_WhenAllConditionsSatisfied()
    {
        var shouldSkip = PaintWindowVisibilityPolicies.ShouldSkip(
            hasOverlayWindow: true,
            hasToolbarWindow: true,
            eventsWired: true,
            shouldWireOverlayLifecycle: false,
            shouldWireToolbarLifecycle: false);

        shouldSkip.Should().BeTrue();
    }

    [Fact]
    public void ShouldSkip_ShouldReturnFalse_WhenAnyConditionNotSatisfied()
    {
        PaintWindowVisibilityPolicies.ShouldSkip(
            hasOverlayWindow: false,
            hasToolbarWindow: true,
            eventsWired: true,
            shouldWireOverlayLifecycle: false,
            shouldWireToolbarLifecycle: false).Should().BeFalse();

        PaintWindowVisibilityPolicies.ShouldSkip(
            hasOverlayWindow: true,
            hasToolbarWindow: true,
            eventsWired: false,
            shouldWireOverlayLifecycle: false,
            shouldWireToolbarLifecycle: false).Should().BeFalse();

        PaintWindowVisibilityPolicies.ShouldSkip(
            hasOverlayWindow: true,
            hasToolbarWindow: true,
            eventsWired: true,
            shouldWireOverlayLifecycle: true,
            shouldWireToolbarLifecycle: false).Should().BeFalse();
    }
}

public sealed class PaintWindowVisibilityPolicyTests
{
    [Fact]
    public void ResolveShow_ShouldShowOverlayAttachToolbarAndRestoreMode_WhenToolbarExists()
    {
        var plan = PaintWindowVisibilityPolicies.ResolveShow(
            overlayVisible: false,
            toolbarExists: true,
            toolbarOwnerAlreadyOverlay: false);

        plan.ShowOverlay.Should().BeTrue();
        plan.ToolbarOwnerAction.Should().Be(FloatingOwnerBindingAction.AttachOverlay);
        plan.ShowToolbar.Should().BeTrue();
        plan.RestoreToolbarMode.Should().BeTrue();
        plan.RestorePresentationFocus.Should().BeTrue();
    }

    [Fact]
    public void ResolveShow_ShouldSkipToolbarActions_WhenToolbarMissing()
    {
        var plan = PaintWindowVisibilityPolicies.ResolveShow(
            overlayVisible: true,
            toolbarExists: false,
            toolbarOwnerAlreadyOverlay: false);

        plan.ShowOverlay.Should().BeFalse();
        plan.ToolbarOwnerAction.Should().Be(FloatingOwnerBindingAction.None);
        plan.ShowToolbar.Should().BeFalse();
    }

    [Fact]
    public void ResolveHide_ShouldHideVisibleOverlayAndToolbar()
    {
        var plan = PaintWindowVisibilityPolicies.ResolveHide(
            overlayVisible: true,
            toolbarVisible: true);

        plan.HideOverlay.Should().BeTrue();
        plan.HideToolbar.Should().BeTrue();
    }

    [Fact]
    public void ResolveHide_ShouldSkipHide_WhenWindowsAlreadyHidden()
    {
        var plan = PaintWindowVisibilityPolicies.ResolveHide(
            overlayVisible: false,
            toolbarVisible: false);

        plan.HideOverlay.Should().BeFalse();
        plan.HideToolbar.Should().BeFalse();
    }

}
