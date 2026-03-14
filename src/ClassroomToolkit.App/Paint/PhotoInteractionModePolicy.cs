namespace ClassroomToolkit.App.Paint;

internal static class PhotoInteractionModePolicy
{
    internal static bool IsPhotoNavigationEnabled(bool photoModeActive, bool boardActive)
    {
        return photoModeActive && !boardActive;
    }

    internal static bool IsPhotoTransformEnabled(bool photoModeActive, bool boardActive)
    {
        return photoModeActive && !boardActive;
    }

    internal static bool IsPhotoOrBoardActive(bool photoModeActive, bool boardActive)
    {
        return photoModeActive || boardActive;
    }

    internal static bool IsCrossPageDisplayActive(
        bool photoModeActive,
        bool boardActive,
        bool crossPageDisplayEnabled)
    {
        return crossPageDisplayEnabled
            && IsPhotoTransformEnabled(photoModeActive, boardActive);
    }
}
