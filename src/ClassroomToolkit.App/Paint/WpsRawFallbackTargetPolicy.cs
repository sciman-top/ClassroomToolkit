namespace ClassroomToolkit.App.Paint;

internal static class WpsRawFallbackTargetPolicy
{
    internal static bool ShouldResolveWpsRawTarget(bool presentationTargetValid, bool allowWps)
    {
        return !presentationTargetValid && allowWps;
    }

    internal static bool IsValid(bool wpsTargetValid, InputStrategy wpsSendMode)
    {
        return wpsTargetValid && wpsSendMode == InputStrategy.Raw;
    }
}
