namespace ClassroomToolkit.App.Windowing;

internal static class WindowStateNormalizationDiagnosticsPolicy
{
    internal static string FormatResolveMessage(WindowStateNormalizationReason reason)
    {
        return $"[WindowStateNormalization] reason={WindowStateNormalizationReasonPolicy.ResolveTag(reason)}";
    }
}
