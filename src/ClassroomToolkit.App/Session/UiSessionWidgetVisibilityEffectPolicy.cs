namespace ClassroomToolkit.App.Session;

internal static class UiSessionWidgetVisibilityEffectPolicy
{
    public static bool ShouldRequestFloatingZOrder(UiSessionWidgetVisibility visibility)
    {
        ArgumentNullException.ThrowIfNull(visibility);

        return visibility.RollCallVisible
            || visibility.LauncherVisible
            || visibility.ToolbarVisible;
    }
}
