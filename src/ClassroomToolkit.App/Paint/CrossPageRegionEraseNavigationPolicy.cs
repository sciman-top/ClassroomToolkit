namespace ClassroomToolkit.App.Paint;

internal readonly record struct CrossPageRegionEraseNavigationPlan(
    bool InteractiveSwitch,
    bool DeferCrossPageDisplayUpdate);

internal static class CrossPageRegionEraseNavigationPolicy
{
    internal static CrossPageRegionEraseNavigationPlan Resolve()
    {
        return new CrossPageRegionEraseNavigationPlan(
            InteractiveSwitch: false,
            DeferCrossPageDisplayUpdate: false);
    }
}
