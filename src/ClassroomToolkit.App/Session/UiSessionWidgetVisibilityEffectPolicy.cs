namespace ClassroomToolkit.App.Session;

public static class UiSessionWidgetVisibilityEffectPolicy
{
    public static bool ShouldRequestFloatingZOrder(UiSessionWidgetVisibility visibility)
    {
        return visibility.RollCallVisible
            || visibility.LauncherVisible
            || visibility.ToolbarVisible;
    }
}
