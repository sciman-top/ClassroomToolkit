namespace ClassroomToolkit.App.Windowing;

internal static class WindowCursorHitTestDiagnosticsPolicy
{
    internal static string FormatResolveMessage(
        WindowCursorHitTestExecutionReason executionReason,
        WindowCursorHitTestReason hitTestReason)
    {
        return $"[WindowCursorHitTest] exec={executionReason} hit={hitTestReason}";
    }
}
