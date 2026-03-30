namespace ClassroomToolkit.App.Windowing;

internal static class UserInitiatedWindowActivationReasonPolicy
{
    internal static string ResolveTag(UserInitiatedWindowActivationReason reason)
    {
        return reason switch
        {
            UserInitiatedWindowActivationReason.WindowNotVisible => "window-not-visible",
            UserInitiatedWindowActivationReason.WindowAlreadyActive => "window-already-active",
            UserInitiatedWindowActivationReason.ActivationRequired => "activation-required",
            _ => "none"
        };
    }
}
