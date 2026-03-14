namespace ClassroomToolkit.App.Windowing;

internal static class RollCallWindowDiagnosticsPolicy
{
    internal static string FormatInitializationFailureMessage(string exceptionType, string message)
    {
        return $"[RollCallWindow] initialization-failed ex={exceptionType} msg={message}";
    }

    internal static string FormatDragMoveFailureMessage(string exceptionType, string message)
    {
        return $"[RollCallWindow] drag-move-failed ex={exceptionType} msg={message}";
    }

    internal static string FormatPhotoOverlayCloseFailureMessage(string operation, string exceptionType, string message)
    {
        return $"[RollCallWindow] photo-overlay-close-failed op={operation} ex={exceptionType} msg={message}";
    }

    internal static string FormatWindowLifecycleFailureMessage(string operation, string exceptionType, string message)
    {
        return $"[RollCallWindow] window-lifecycle-failed op={operation} ex={exceptionType} msg={message}";
    }

    internal static string FormatDialogShowFailureMessage(string dialogName, string exceptionType, string message)
    {
        return $"[RollCallWindow] dialog-show-failed dialog={dialogName} ex={exceptionType} msg={message}";
    }

    internal static string FormatGroupOverlayFailureMessage(string operation, string exceptionType, string message)
    {
        return $"[RollCallWindow] group-overlay-failed op={operation} ex={exceptionType} msg={message}";
    }

    internal static string FormatConfirmationFailureMessage(string operation, string exceptionType, string message)
    {
        return $"[RollCallWindow] confirm-show-failed op={operation} ex={exceptionType} msg={message}";
    }
}
