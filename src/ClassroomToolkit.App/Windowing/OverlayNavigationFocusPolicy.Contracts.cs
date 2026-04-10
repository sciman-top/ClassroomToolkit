namespace ClassroomToolkit.App.Windowing;

internal readonly record struct OverlayNavigationFocusPlan(
    bool ActivateOverlay,
    bool KeyboardFocusOverlay);

internal readonly record struct OverlayNavigationFocusPlanDecision(
    OverlayNavigationFocusPlan Plan,
    OverlayNavigationActivateReason ActivateReason,
    OverlayNavigationKeyboardFocusReason KeyboardFocusReason);

internal enum OverlayNavigationActivateReason
{
    None = 0,
    AvoidActivateRequested = 1,
    OverlayAlreadyActive = 2,
    BlockedByToolbar = 3,
    BlockedByRollCall = 4,
    BlockedByImageManager = 5,
    BlockedByLauncher = 6
}

internal readonly record struct OverlayNavigationActivateDecision(
    bool ShouldActivateOverlay,
    OverlayNavigationActivateReason Reason);

internal enum OverlayNavigationKeyboardFocusReason
{
    None = 0,
    OverlayNotVisible = 1,
    BlockedByToolbar = 2,
    BlockedByRollCall = 3,
    BlockedByImageManager = 4,
    BlockedByLauncher = 5
}

internal readonly record struct OverlayNavigationKeyboardFocusDecision(
    bool ShouldKeyboardFocusOverlay,
    OverlayNavigationKeyboardFocusReason Reason);
