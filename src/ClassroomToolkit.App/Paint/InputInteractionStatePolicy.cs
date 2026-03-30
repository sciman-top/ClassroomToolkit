namespace ClassroomToolkit.App.Paint;

internal readonly record struct InputInteractionState(
    bool PhotoModeActive,
    bool BoardActive,
    bool CrossPageDisplayEnabled)
{
    internal bool PhotoOrBoardActive => PhotoInteractionModePolicy.IsPhotoOrBoardActive(PhotoModeActive, BoardActive);
    internal bool PhotoNavigationEnabled => PhotoInteractionModePolicy.IsPhotoNavigationEnabled(PhotoModeActive, BoardActive);
    internal bool CrossPageDisplayActive => CrossPageInputDisplayPolicy.IsActive(
        PhotoModeActive,
        BoardActive,
        CrossPageDisplayEnabled);
    internal bool CrossPageInputDisplayActive => CrossPageDisplayActive;
}

internal static class InputInteractionStatePolicy
{
    internal static InputInteractionState Resolve(
        bool photoModeActive,
        bool boardActive,
        bool crossPageDisplayEnabled)
    {
        return new InputInteractionState(
            PhotoModeActive: photoModeActive,
            BoardActive: boardActive,
            CrossPageDisplayEnabled: crossPageDisplayEnabled);
    }
}
