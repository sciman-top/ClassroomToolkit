namespace ClassroomToolkit.App.Windowing;

internal readonly record struct PhotoOverlayEntryPlan(
    bool UpdateSequence,
    bool UpdateInkVisibility,
    bool SuppressNextOverlayActivatedApply,
    bool EnterPhotoMode,
    bool TouchPhotoSurface,
    bool FocusOverlay);

internal static class PhotoOverlayEntryPolicy
{
    internal static PhotoOverlayEntryPlan Resolve(bool hasPath)
    {
        if (!hasPath)
        {
            return new PhotoOverlayEntryPlan(
                UpdateSequence: true,
                UpdateInkVisibility: false,
                SuppressNextOverlayActivatedApply: false,
                EnterPhotoMode: false,
                TouchPhotoSurface: false,
                FocusOverlay: false);
        }

        return new PhotoOverlayEntryPlan(
            UpdateSequence: true,
            UpdateInkVisibility: true,
            SuppressNextOverlayActivatedApply: true,
            EnterPhotoMode: true,
            TouchPhotoSurface: true,
            FocusOverlay: true);
    }
}
