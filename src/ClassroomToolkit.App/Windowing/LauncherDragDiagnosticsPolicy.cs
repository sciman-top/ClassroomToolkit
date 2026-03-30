namespace ClassroomToolkit.App.Windowing;

internal static class LauncherDragDiagnosticsPolicy
{
    internal static string FormatDragMoveFailureMessage(string exceptionType, string message)
    {
        return $"[LauncherDrag] drag-move-failed ex={exceptionType} msg={message}";
    }
}
