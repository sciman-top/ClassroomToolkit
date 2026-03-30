namespace ClassroomToolkit.App.Windowing;

internal readonly record struct PhotoSelectionPreparationPlan(
    bool CloseImageManager,
    bool DisableWhiteboard,
    bool SuppressPresentationForeground,
    int PresentationForegroundSuppressionMs);

internal static class PhotoSelectionPreparationPolicy
{
    internal static PhotoSelectionPreparationPlan Resolve(
        bool imageManagerVisible,
        bool whiteboardActive)
    {
        return new PhotoSelectionPreparationPlan(
            CloseImageManager: imageManagerVisible,
            DisableWhiteboard: whiteboardActive,
            SuppressPresentationForeground: true,
            PresentationForegroundSuppressionMs: PhotoSelectionPreparationDefaults.PresentationForegroundSuppressionMs);
    }
}
