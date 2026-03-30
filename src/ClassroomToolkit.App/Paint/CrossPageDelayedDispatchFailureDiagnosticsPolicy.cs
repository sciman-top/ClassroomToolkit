namespace ClassroomToolkit.App.Paint;

internal static class CrossPageDelayedDispatchFailureDiagnosticsPolicy
{
    internal static string FormatDelayFailureDetail(string exceptionType)
    {
        return $"delayed-delay-failed ex={exceptionType}";
    }

    internal static string FormatInlineRecoveryDetail(bool tokenMatched)
    {
        return tokenMatched
            ? "delayed-delay-failed-inline-recovered"
            : "delayed-delay-failed-inline-skip-token-mismatch";
    }
}
