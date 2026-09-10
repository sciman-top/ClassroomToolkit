namespace ClassroomToolkit.App.Paint;

internal static class PointerCaptureCleanupPolicy
{
    internal static bool ShouldDeferCleanup(
        string reason,
        bool mouseCaptured,
        bool stylusCaptured)
    {
        if (!string.Equals(reason, "mouse-capture-lost", StringComparison.Ordinal)
            && !string.Equals(reason, "stylus-capture-lost", StringComparison.Ordinal))
        {
            return false;
        }

        return mouseCaptured || stylusCaptured;
    }
}
