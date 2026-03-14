namespace ClassroomToolkit.App;

internal enum MainWindowLoadedToggleAction
{
    MinimizeLauncher,
    UpdateToggleButtons
}

internal static class MainWindowLoadedToggleActionPolicy
{
    internal static MainWindowLoadedToggleAction Resolve(bool launcherMinimized)
    {
        return launcherMinimized
            ? MainWindowLoadedToggleAction.MinimizeLauncher
            : MainWindowLoadedToggleAction.UpdateToggleButtons;
    }
}
