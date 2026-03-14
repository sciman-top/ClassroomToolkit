namespace ClassroomToolkit.App.Paint;

internal static class CrossPageDuplicateWindowReasonPolicy
{
    internal static string ResolveDiagnosticTag(CrossPageDuplicateWindowSkipReason reason)
    {
        return reason switch
        {
            CrossPageDuplicateWindowSkipReason.BackgroundRefresh => "background-duplicate-window",
            CrossPageDuplicateWindowSkipReason.Interaction => "interaction-duplicate-window",
            _ => "duplicate-window"
        };
    }
}
