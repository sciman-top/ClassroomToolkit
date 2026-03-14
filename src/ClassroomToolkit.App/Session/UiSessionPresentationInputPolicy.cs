namespace ClassroomToolkit.App.Session;

public static class UiSessionPresentationInputPolicy
{
    public static bool AllowsPresentationInput(UiNavigationMode navigationMode)
    {
        return navigationMode is UiNavigationMode.Hybrid or UiNavigationMode.HookOnly;
    }
}
