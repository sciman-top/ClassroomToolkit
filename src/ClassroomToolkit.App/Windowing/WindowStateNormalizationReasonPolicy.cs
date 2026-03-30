namespace ClassroomToolkit.App.Windowing;

internal static class WindowStateNormalizationReasonPolicy
{
    internal static string ResolveTag(WindowStateNormalizationReason reason)
    {
        return reason switch
        {
            WindowStateNormalizationReason.TargetMissing => "target-missing",
            WindowStateNormalizationReason.NormalizationNotRequested => "normalization-not-requested",
            WindowStateNormalizationReason.NormalizationRequested => "normalization-requested",
            _ => "none"
        };
    }
}
