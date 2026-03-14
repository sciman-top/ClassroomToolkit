namespace ClassroomToolkit.App.Windowing;

internal static class LauncherWindowRuntimeDiagnosticsPolicy
{
    internal static string FormatSelectionMessage(LauncherWindowRuntimeSelectionReason reason)
    {
        return $"[Launcher][Snapshot] selection={LauncherWindowRuntimeSelectionReasonPolicy.ResolveTag(reason)}";
    }
}
