using ClassroomToolkit.App.Paint;

namespace ClassroomToolkit.App.Windowing;

internal enum PhotoCursorModeFocusRequestReason
{
    None = 0,
    PhotoModeInactive = 1,
    ToolModeNotCursor = 2,
    FocusRequested = 3
}

internal readonly record struct PhotoCursorModeFocusRequestDecision(
    bool ShouldRequestFocus,
    PhotoCursorModeFocusRequestReason Reason);

internal static class PhotoCursorModeFocusRequestPolicy
{
    internal static PhotoCursorModeFocusRequestDecision Resolve(bool photoModeActive, PaintToolMode mode)
    {
        if (!photoModeActive)
        {
            return new PhotoCursorModeFocusRequestDecision(
                ShouldRequestFocus: false,
                Reason: PhotoCursorModeFocusRequestReason.PhotoModeInactive);
        }

        if (mode != PaintToolMode.Cursor)
        {
            return new PhotoCursorModeFocusRequestDecision(
                ShouldRequestFocus: false,
                Reason: PhotoCursorModeFocusRequestReason.ToolModeNotCursor);
        }

        return new PhotoCursorModeFocusRequestDecision(
            ShouldRequestFocus: true,
            Reason: PhotoCursorModeFocusRequestReason.FocusRequested);
    }

    internal static bool ShouldRequestFocus(bool photoModeActive, PaintToolMode mode)
    {
        return Resolve(photoModeActive, mode).ShouldRequestFocus;
    }
}
