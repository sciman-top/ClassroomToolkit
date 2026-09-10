using ClassroomToolkit.Interop.Presentation;

namespace ClassroomToolkit.App.Paint;

internal static class PresentationTargetChannelSelectionPolicy
{
    internal static PresentationType ResolveForFocus(
        PresentationType foregroundType,
        bool foregroundIsFullscreen,
        PresentationType currentPresentationType,
        bool allowWps,
        bool allowOffice)
    {
        if (foregroundIsFullscreen && IsAllowed(foregroundType, allowWps, allowOffice))
        {
            return foregroundType;
        }

        if (IsAllowed(currentPresentationType, allowWps, allowOffice))
        {
            return currentPresentationType;
        }

        if (allowWps && !allowOffice)
        {
            return PresentationType.Wps;
        }

        if (allowOffice && !allowWps)
        {
            return PresentationType.Office;
        }

        // With both channels enabled and no foreground/current-session evidence,
        // do not silently choose one application over the other.
        return PresentationType.None;
    }

    private static bool IsAllowed(
        PresentationType type,
        bool allowWps,
        bool allowOffice)
    {
        return (type == PresentationType.Wps && allowWps)
            || (type == PresentationType.Office && allowOffice);
    }
}
