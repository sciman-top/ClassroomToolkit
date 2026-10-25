using ClassroomToolkit.App.Windowing;
using AwesomeAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.Windowing;

public sealed class OverlayNavigationFocusPolicyTests
{
    private static readonly FloatingUtilityActivitySnapshot NoUtilityActivity = new(
        ToolbarActive: false,
        RollCallActive: false,
        ImageManagerActive: false,
        LauncherActive: false);

    [Fact]
    public void ShouldActivateOverlay_ReturnsTrue_WhenOverlayNeedsFocus_AndNoUtilityWindowIsActive()
    {
        OverlayNavigationFocusPolicy
            .ShouldActivateOverlay(
                avoidActivate: false,
                overlayActive: false,
                NoUtilityActivity)
            .Should()
            .BeTrue();
    }

    [Fact]
    public void ShouldActivateOverlay_ReturnsFalse_WhenAvoidActivateRequested()
    {
        OverlayNavigationFocusPolicy
            .ShouldActivateOverlay(
                avoidActivate: true,
                overlayActive: false,
                NoUtilityActivity)
            .Should()
            .BeFalse();
    }

    [Fact]
    public void ShouldActivateOverlay_ReturnsFalse_WhenOverlayAlreadyActive()
    {
        OverlayNavigationFocusPolicy
            .ShouldActivateOverlay(
                avoidActivate: false,
                overlayActive: true,
                NoUtilityActivity)
            .Should()
            .BeFalse();
    }

    [Theory]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, false, false, true)]
    public void ShouldActivateOverlay_ReturnsFalse_WhenAnyUtilityWindowIsActive(
        bool toolbarActive,
        bool rollCallActive,
        bool imageManagerActive,
        bool launcherActive)
    {
        OverlayNavigationFocusPolicy
            .ShouldActivateOverlay(
                avoidActivate: false,
                overlayActive: false,
                toolbarActive,
                rollCallActive,
                imageManagerActive,
                launcherActive)
            .Should()
            .BeFalse();
    }

    [Fact]
    public void ShouldKeyboardFocusOverlay_ReturnsTrue_WhenOverlayVisible_AndNoUtilityWindowIsActive()
    {
        OverlayNavigationFocusPolicy
            .ShouldKeyboardFocusOverlay(overlayVisible: true, NoUtilityActivity)
            .Should()
            .BeTrue();
    }

    [Fact]
    public void ShouldKeyboardFocusOverlay_ReturnsFalse_WhenOverlayNotVisible()
    {
        OverlayNavigationFocusPolicy
            .ShouldKeyboardFocusOverlay(overlayVisible: false, NoUtilityActivity)
            .Should()
            .BeFalse();
    }

    [Fact]
    public void ShouldKeyboardFocusOverlay_ReturnsFalse_WhenBlockedUtilityWindowIsActive_ButIgnoresTapToActivate()
    {
        // Keyboard focus intentionally excludes avoidActivate: a photo-navigation
        // request may still move focus without raising the overlay.
        OverlayNavigationFocusPolicy
            .ShouldKeyboardFocusOverlay(
                overlayVisible: true,
                toolbarActive: true,
                rollCallActive: false,
                imageManagerActive: false,
                launcherActive: false)
            .Should()
            .BeFalse();

        OverlayNavigationFocusPolicy
            .ShouldActivateOverlay(
                avoidActivate: true,
                overlayActive: false,
                NoUtilityActivity)
            .Should()
            .BeFalse();
    }
}
