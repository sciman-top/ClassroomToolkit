namespace ClassroomToolkit.App;

internal readonly record struct MainWindowOnClosingPlan(
    bool ShouldCancelClose,
    bool ShouldRequestExit);

internal static class MainWindowOnClosingPlanPolicy
{
    internal static MainWindowOnClosingPlan Resolve(bool allowClose)
    {
        if (allowClose)
        {
            return new MainWindowOnClosingPlan(
                ShouldCancelClose: false,
                ShouldRequestExit: false);
        }

        return new MainWindowOnClosingPlan(
            ShouldCancelClose: true,
            ShouldRequestExit: true);
    }
}
