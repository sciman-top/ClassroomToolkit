namespace ClassroomToolkit.App.Windowing;

internal enum UserInitiatedWindowActivationReason
{
    None = 0,
    WindowNotVisible = 1,
    WindowAlreadyActive = 2,
    ActivationRequired = 3
}

internal readonly record struct UserInitiatedWindowActivationDecision(
    bool ShouldActivateAfterShow,
    UserInitiatedWindowActivationReason Reason);

internal static class UserInitiatedWindowActivationPolicy
{
    internal static UserInitiatedWindowActivationDecision Resolve(
        bool windowVisible,
        bool windowActive)
    {
        if (!windowVisible)
        {
            return new UserInitiatedWindowActivationDecision(
                ShouldActivateAfterShow: false,
                Reason: UserInitiatedWindowActivationReason.WindowNotVisible);
        }

        if (windowActive)
        {
            return new UserInitiatedWindowActivationDecision(
                ShouldActivateAfterShow: false,
                Reason: UserInitiatedWindowActivationReason.WindowAlreadyActive);
        }

        return new UserInitiatedWindowActivationDecision(
            ShouldActivateAfterShow: true,
            Reason: UserInitiatedWindowActivationReason.ActivationRequired);
    }

    internal static bool ShouldActivateAfterShow(bool windowVisible, bool windowActive)
    {
        return Resolve(windowVisible, windowActive).ShouldActivateAfterShow;
    }
}
