namespace ClassroomToolkit.App.Paint;

internal static class PhotoManipulationInertiaPolicy
{
    internal static double ResolveTranslationDeceleration(bool crossPageDisplayActive)
    {
        return ResolveTranslationDeceleration(
            crossPageDisplayActive,
            PhotoPanInertiaTuning.Default);
    }

    internal static double ResolveTranslationDeceleration(
        bool crossPageDisplayActive,
        PhotoPanInertiaTuning tuning)
    {
        return crossPageDisplayActive
            ? tuning.GestureCrossPageTranslationDecelerationDipPerMs2
            : tuning.GestureTranslationDecelerationDipPerMs2;
    }
}
