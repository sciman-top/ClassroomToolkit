namespace ClassroomToolkit.App;

internal readonly record struct MainWindowExitPlan(
    bool ShouldExit,
    bool ShouldCancelBackgroundTasks,
    bool ShouldCloseBubbleWindow,
    bool ShouldCloseRollCallWindow);

internal static class MainWindowExitPlanPolicy
{
    internal static MainWindowExitPlan Resolve(
        bool allowClose,
        bool backgroundTasksCancellationRequested,
        bool hasBubbleWindow,
        bool hasRollCallWindow)
    {
        if (allowClose)
        {
            return new MainWindowExitPlan(
                ShouldExit: false,
                ShouldCancelBackgroundTasks: false,
                ShouldCloseBubbleWindow: false,
                ShouldCloseRollCallWindow: false);
        }

        return new MainWindowExitPlan(
            ShouldExit: true,
            ShouldCancelBackgroundTasks: !backgroundTasksCancellationRequested,
            ShouldCloseBubbleWindow: hasBubbleWindow,
            ShouldCloseRollCallWindow: hasRollCallWindow);
    }
}
