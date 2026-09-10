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

        // HWND values can be recycled after a window closes.  The process id,
        // executable name, and native class identity must all still describe
        // the cached target before input is admitted.
        if (target.Info.ProcessId == 0
            || currentCheck.ProcessId == 0
            || target.Info.ProcessId != currentCheck.ProcessId
            || !string.Equals(
                NormalizeIdentity(target.Info.ProcessName),
                NormalizeIdentity(currentCheck.ProcessName),
                StringComparison.OrdinalIgnoreCase)
            || !ClassIdentityMatches(target.Info.ClassNames, currentCheck.ClassNames))
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

        if (currentCheck.ClassMatch)
        {
            return true;
        }

        return PresentationFullscreenWindowAdmissionPolicy.ShouldTreatAsPresentationFullscreen(
            targetIsValid: target.IsValid,
            targetHasInfo: target.Info != null,
            isFullscreen: currentCheck.IsFullscreen,
            classifiesAsSlideshow: false,
            classifiesAsOffice: currentCheck.Type == PresentationType.Office,
            classifiesAsDedicatedWpsRuntime: currentCheck.Type == PresentationType.Wps
                && WpsPresentationRuntimePolicy.IsDedicatedSlideshowRuntime(currentCheck.ProcessName));
    }

    private static bool ClassIdentityMatches(
        IReadOnlyList<string>? cachedClasses,
        IReadOnlyList<string>? currentClasses)
    {
        var cached = NormalizeClasses(cachedClasses);
        var current = NormalizeClasses(currentClasses);
        return cached.Count > 0
            && cached.Count == current.Count
            && cached.SetEquals(current);
    }

    private static HashSet<string> NormalizeClasses(IReadOnlyList<string>? classes)
    {
        var normalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (classes == null)
        {
            return normalized;
        }

        for (var i = 0; i < classes.Count; i++)
        {
            var value = NormalizeIdentity(classes[i]);
            if (!string.IsNullOrWhiteSpace(value))
            {
                normalized.Add(value);
            }
        }

        return normalized;
    }

    private static string NormalizeIdentity(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
