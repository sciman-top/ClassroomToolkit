namespace ClassroomToolkit.App.Windowing;

internal static class FloatingTopmostForceEnforceReasonPolicy
{
    internal static string ResolveTag(FloatingTopmostForceEnforceReason reason)
    {
        return reason switch
        {
            FloatingTopmostForceEnforceReason.DisabledByDesign => "disabled-by-design",
            _ => "none"
        };
    }
}
