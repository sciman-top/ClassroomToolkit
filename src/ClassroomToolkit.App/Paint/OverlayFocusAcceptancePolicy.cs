using ClassroomToolkit.App.Session;

namespace ClassroomToolkit.App.Paint;

internal static class OverlayFocusAcceptancePolicy
{
    internal static bool ShouldBlockFocus(
        UiNavigationMode navigationMode,
        bool inputPassthroughEnabled,
        PaintToolMode mode,
        bool photoModeActive,
        bool boardActive,
        bool presentationAllowed,
        bool presentationTargetValid,
        bool wpsRawTargetValid)
    {
        if (mode != PaintToolMode.Cursor)
        {
            return false;
        }

        if (photoModeActive || boardActive)
        {
            return false;
        }

        if (inputPassthroughEnabled)
        {
            return true;
        }

        if (!presentationAllowed)
        {
            return false;
        }

        if (!UiSessionPresentationInputPolicy.AllowsPresentationInput(navigationMode))
        {
            return false;
        }

        return presentationTargetValid || wpsRawTargetValid;
    }
}
