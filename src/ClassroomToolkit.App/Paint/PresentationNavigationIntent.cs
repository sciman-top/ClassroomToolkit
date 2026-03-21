namespace ClassroomToolkit.App.Paint;

internal enum PresentationNavigationSource
{
    Unknown = 0,
    HookKeyboard = 1,
    HookWheel = 2,
    OverlayKeyboard = 3,
    OverlayWheel = 4,
    DirectCommand = 5
}

internal readonly record struct PresentationNavigationIntent(
    int Direction,
    PresentationNavigationSource Source)
{
    internal static readonly PresentationNavigationIntent None =
        new(0, PresentationNavigationSource.Unknown);

    internal bool IsKeyboardSource =>
        Source == PresentationNavigationSource.HookKeyboard
        || Source == PresentationNavigationSource.OverlayKeyboard;

    internal bool IsWheelSource =>
        Source == PresentationNavigationSource.HookWheel
        || Source == PresentationNavigationSource.OverlayWheel;

    internal static bool TryCreatePageTurn(
        int direction,
        PresentationNavigationSource source,
        out PresentationNavigationIntent intent)
    {
        if (direction is not -1 and not 1)
        {
            intent = None;
            return false;
        }

        intent = new PresentationNavigationIntent(direction, source);
        return true;
    }
}
