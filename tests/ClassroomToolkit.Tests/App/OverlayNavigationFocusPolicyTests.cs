using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class OverlayNavigationFocusPolicyTests
{
    [Fact]
    public void ResolveActivateDecision_ShouldReturnAvoidActivateRequested_WhenAvoidActivateIsTrue()
    {
        var decision = OverlayNavigationFocusPolicy.ResolveActivateDecision(
            avoidActivate: true,
            overlayActive: false,
            utilityActivity: new FloatingUtilityActivitySnapshot(
                ToolbarActive: false,
                RollCallActive: false,
                ImageManagerActive: false,
                LauncherActive: false));

        decision.ShouldActivateOverlay.Should().BeFalse();
        decision.Reason.Should().Be(OverlayNavigationActivateReason.AvoidActivateRequested);
    }

    [Fact]
    public void ResolveKeyboardFocusDecision_ShouldReturnOverlayNotVisible_WhenOverlayHidden()
    {
        var decision = OverlayNavigationFocusPolicy.ResolveKeyboardFocusDecision(
            overlayVisible: false,
            utilityActivity: new FloatingUtilityActivitySnapshot(
                ToolbarActive: false,
                RollCallActive: false,
                ImageManagerActive: false,
                LauncherActive: false));

        decision.ShouldKeyboardFocusOverlay.Should().BeFalse();
        decision.Reason.Should().Be(OverlayNavigationKeyboardFocusReason.OverlayNotVisible);
    }

    [Fact]
    public void ShouldActivateOverlay_ShouldReturnTrue_WhenOverlayNeedsFocus_AndNoUtilityWindowIsActive()
    {
        var shouldActivate = OverlayNavigationFocusPolicy.ShouldActivateOverlay(
            avoidActivate: false,
            overlayActive: false,
            toolbarActive: false,
            rollCallActive: false,
            imageManagerActive: false,
            launcherActive: false);

        shouldActivate.Should().BeTrue();
    }

    [Fact]
    public void ShouldActivateOverlay_ShouldReturnFalse_WhenAvoidActivateRequested()
    {
        var shouldActivate = OverlayNavigationFocusPolicy.ShouldActivateOverlay(
            avoidActivate: true,
            overlayActive: false,
            toolbarActive: false,
            rollCallActive: false,
            imageManagerActive: false,
            launcherActive: false);

        shouldActivate.Should().BeFalse();
    }

    [Fact]
    public void ShouldActivateOverlay_ShouldReturnFalse_WhenOverlayAlreadyActive()
    {
        var shouldActivate = OverlayNavigationFocusPolicy.ShouldActivateOverlay(
            avoidActivate: false,
            overlayActive: true,
            toolbarActive: false,
            rollCallActive: false,
            imageManagerActive: false,
            launcherActive: false);

        shouldActivate.Should().BeFalse();
    }

    [Theory]
    [InlineData(true, false, false, false, (int)OverlayNavigationActivateReason.BlockedByToolbar)]
    [InlineData(false, true, false, false, (int)OverlayNavigationActivateReason.BlockedByRollCall)]
    [InlineData(false, false, true, false, (int)OverlayNavigationActivateReason.BlockedByImageManager)]
    [InlineData(false, false, false, true, (int)OverlayNavigationActivateReason.BlockedByLauncher)]
    public void ShouldActivateOverlay_ShouldReturnFalse_WhenAnyUtilityWindowIsActive(
        bool toolbarActive,
        bool rollCallActive,
        bool imageManagerActive,
        bool launcherActive,
        int expectedReason)
    {
        var decision = OverlayNavigationFocusPolicy.ResolveActivateDecision(
            avoidActivate: false,
            overlayActive: false,
            utilityActivity: new FloatingUtilityActivitySnapshot(
                ToolbarActive: toolbarActive,
                RollCallActive: rollCallActive,
                ImageManagerActive: imageManagerActive,
                LauncherActive: launcherActive));

        decision.ShouldActivateOverlay.Should().BeFalse();
        decision.Reason.Should().Be((OverlayNavigationActivateReason)expectedReason);
    }

    [Fact]
    public void ShouldKeyboardFocusOverlay_ShouldReturnTrue_WhenOverlayVisible_AndNoUtilityWindowIsActive()
    {
        var shouldFocus = OverlayNavigationFocusPolicy.ShouldKeyboardFocusOverlay(
            overlayVisible: true,
            toolbarActive: false,
            rollCallActive: false,
            imageManagerActive: false,
            launcherActive: false);

        shouldFocus.Should().BeTrue();
    }

    [Fact]
    public void ShouldKeyboardFocusOverlay_ShouldReturnFalse_WhenOverlayNotVisible()
    {
        var shouldFocus = OverlayNavigationFocusPolicy.ShouldKeyboardFocusOverlay(
            overlayVisible: false,
            toolbarActive: false,
            rollCallActive: false,
            imageManagerActive: false,
            launcherActive: false);

        shouldFocus.Should().BeFalse();
    }

    [Theory]
    [InlineData(true, false, false, false, (int)OverlayNavigationKeyboardFocusReason.BlockedByToolbar)]
    [InlineData(false, true, false, false, (int)OverlayNavigationKeyboardFocusReason.BlockedByRollCall)]
    [InlineData(false, false, true, false, (int)OverlayNavigationKeyboardFocusReason.BlockedByImageManager)]
    [InlineData(false, false, false, true, (int)OverlayNavigationKeyboardFocusReason.BlockedByLauncher)]
    public void ShouldKeyboardFocusOverlay_ShouldReturnFalse_WhenAnyUtilityWindowIsActive(
        bool toolbarActive,
        bool rollCallActive,
        bool imageManagerActive,
        bool launcherActive,
        int expectedReason)
    {
        var decision = OverlayNavigationFocusPolicy.ResolveKeyboardFocusDecision(
            overlayVisible: true,
            utilityActivity: new FloatingUtilityActivitySnapshot(
                ToolbarActive: toolbarActive,
                RollCallActive: rollCallActive,
                ImageManagerActive: imageManagerActive,
                LauncherActive: launcherActive));

        decision.ShouldKeyboardFocusOverlay.Should().BeFalse();
        decision.Reason.Should().Be((OverlayNavigationKeyboardFocusReason)expectedReason);
    }

    [Fact]
    public void ShouldActivateOverlay_ShouldSupportUnifiedUtilitySnapshot()
    {
        var shouldActivate = OverlayNavigationFocusPolicy.ShouldActivateOverlay(
            avoidActivate: false,
            overlayActive: false,
            utilityActivity: new FloatingUtilityActivitySnapshot(
                ToolbarActive: false,
                RollCallActive: false,
                ImageManagerActive: false,
                LauncherActive: false));

        shouldActivate.Should().BeTrue();
    }

    [Fact]
    public void ShouldKeyboardFocusOverlay_ShouldSupportUnifiedUtilitySnapshot()
    {
        var shouldFocus = OverlayNavigationFocusPolicy.ShouldKeyboardFocusOverlay(
            overlayVisible: true,
            utilityActivity: new FloatingUtilityActivitySnapshot(
                ToolbarActive: false,
                RollCallActive: false,
                ImageManagerActive: false,
                LauncherActive: false));

        shouldFocus.Should().BeTrue();
    }

    [Fact]
    public void ResolvePlan_ShouldReturnExpectedFocusActions_WhenOverlayVisible_AndNoUtilityWindowIsActive()
    {
        var plan = OverlayNavigationFocusPolicy.ResolvePlan(
            avoidActivate: false,
            overlayVisible: true,
            overlayActive: false,
            utilityActivity: new FloatingUtilityActivitySnapshot(
                ToolbarActive: false,
                RollCallActive: false,
                ImageManagerActive: false,
                LauncherActive: false));

        plan.ActivateOverlay.Should().BeTrue();
        plan.KeyboardFocusOverlay.Should().BeTrue();
    }

    [Fact]
    public void ResolvePlan_ShouldBlockActivation_ButStillAllowKeyboardFocus_WhenAvoidActivateRequested()
    {
        var plan = OverlayNavigationFocusPolicy.ResolvePlan(
            avoidActivate: true,
            overlayVisible: true,
            overlayActive: false,
            utilityActivity: new FloatingUtilityActivitySnapshot(
                ToolbarActive: false,
                RollCallActive: false,
                ImageManagerActive: false,
                LauncherActive: false));

        plan.ActivateOverlay.Should().BeFalse();
        plan.KeyboardFocusOverlay.Should().BeTrue();
    }

    [Fact]
    public void ResolvePlanDecision_ShouldContainActivateAndKeyboardReasons()
    {
        var decision = OverlayNavigationFocusPolicy.ResolvePlanDecision(
            avoidActivate: false,
            snapshot: new OverlayNavigationFocusSnapshot(
                OverlayVisible: true,
                OverlayActive: false,
                UtilityActivity: new FloatingUtilityActivitySnapshot(
                    ToolbarActive: true,
                    RollCallActive: false,
                    ImageManagerActive: false,
                    LauncherActive: false)));

        decision.Plan.ActivateOverlay.Should().BeFalse();
        decision.ActivateReason.Should().Be(OverlayNavigationActivateReason.BlockedByToolbar);
        decision.Plan.KeyboardFocusOverlay.Should().BeFalse();
        decision.KeyboardFocusReason.Should().Be(OverlayNavigationKeyboardFocusReason.BlockedByToolbar);
    }

    [Fact]
    public void ShouldActivateOverlay_ShouldMapResolveDecision()
    {
        OverlayNavigationFocusPolicy.ShouldActivateOverlay(
            avoidActivate: false,
            overlayActive: false,
            toolbarActive: false,
            rollCallActive: false,
            imageManagerActive: false,
            launcherActive: false).Should().BeTrue();
    }

    [Fact]
    public void ShouldKeyboardFocusOverlay_ShouldMapResolveDecision()
    {
        OverlayNavigationFocusPolicy.ShouldKeyboardFocusOverlay(
            overlayVisible: true,
            toolbarActive: false,
            rollCallActive: false,
            imageManagerActive: false,
            launcherActive: false).Should().BeTrue();
    }
}
