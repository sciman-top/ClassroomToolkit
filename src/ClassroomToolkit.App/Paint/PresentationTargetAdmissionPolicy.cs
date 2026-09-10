using ClassroomToolkit.Interop.Presentation;

namespace ClassroomToolkit.App.Paint;

internal static class PresentationTargetAdmissionPolicy
{
    internal static bool IsFreshIdentityMatch(
        PresentationTarget target,
        PresentationWindowCheck? currentCheck,
        PresentationClassifier classifier,
        PresentationType? expectedType)
    {
        ArgumentNullException.ThrowIfNull(classifier);

        if (!target.IsValid || target.Info == null || currentCheck == null)
        {
            return false;
        }

        if (currentCheck.Type is PresentationType.None or PresentationType.Other)
        {
            return false;
        }

        // The cached PresentationTarget carries the metadata used by the planner.
        // A recycled HWND must not be allowed to keep the old channel identity.
        var cachedType = classifier.Classify(target.Info);
        if (currentCheck.Type != cachedType)
        {
            return false;
        }

        if (expectedType.HasValue && currentCheck.Type != expectedType.Value)
        {
            return false;
        }

        return currentCheck.ClassMatch || currentCheck.IsFullscreen;
    }
}
