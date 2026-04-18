using ClassroomToolkit.App.Session;

namespace ClassroomToolkit.App.Paint;

internal static class PresentationFocusRestorePolicy
{
    internal static bool CanRestore(
        UiSessionState sessionState,
        bool photoModeActive,
        bool boardActive,
        bool isVisible,
        bool presentationAllowed,
        bool targetIsValid,
        bool targetIsSlideshow,
        bool targetIsFullscreen,
        bool requireFullscreen,
        bool forceForeground,
        bool foregroundOwnedByCurrentProcess,
        bool dragOperationActive)
    {
        if (!isVisible || photoModeActive || boardActive || dragOperationActive)
        {
            return false;
        }

        if (!presentationAllowed)
        {
            return false;
        }

        if (sessionState.ToolMode != UiToolMode.Cursor)
        {
            return false;
        }

        if (!UiSessionPresentationInputPolicy.AllowsPresentationInput(sessionState.NavigationMode))
        {
            return false;
        }

        if (!targetIsValid || !targetIsSlideshow)
        {
            return false;
        }

        if (requireFullscreen && !targetIsFullscreen)
        {
            return false;
        }

        if (!forceForeground && !foregroundOwnedByCurrentProcess)
        {
            return false;
        }

        return true;
    }
}
