namespace ClassroomToolkit.App.Paint;

internal readonly record struct CrossPagePointerUpState(
    bool CrossPageDisplayActive,
    bool PhotoTransformActive);

internal static class CrossPagePointerUpStatePolicy
{
    internal static CrossPagePointerUpState Resolve(
        bool photoModeActive,
        bool boardActive,
        bool crossPageDisplayEnabled)
    {
        var photoTransformActive = PhotoInteractionModePolicy.IsPhotoTransformEnabled(
            photoModeActive,
            boardActive);
        var crossPageDisplayActive = crossPageDisplayEnabled && photoTransformActive;

        return new CrossPagePointerUpState(
            CrossPageDisplayActive: crossPageDisplayActive,
            PhotoTransformActive: photoTransformActive);
    }
}
