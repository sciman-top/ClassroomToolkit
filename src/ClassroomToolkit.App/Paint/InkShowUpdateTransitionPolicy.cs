namespace ClassroomToolkit.App.Paint;

internal readonly record struct InkShowUpdateTransitionPlan(
    bool ShouldApplySetting,
    bool ShouldReturnAfterSetting,
    bool ShouldClearInkState,
    bool ShouldLoadCurrentPage,
    bool RequestCrossPageUpdateForEnabled,
    bool RequestCrossPageUpdateForDisabled);

internal static class InkShowUpdateTransitionPolicy
{
    internal static InkShowUpdateTransitionPlan Resolve(
        bool currentInkShowEnabled,
        bool nextInkShowEnabled,
        bool photoModeActive)
    {
        if (currentInkShowEnabled == nextInkShowEnabled)
        {
            return new InkShowUpdateTransitionPlan(
                ShouldApplySetting: false,
                ShouldReturnAfterSetting: true,
                ShouldClearInkState: false,
                ShouldLoadCurrentPage: false,
                RequestCrossPageUpdateForEnabled: false,
                RequestCrossPageUpdateForDisabled: false);
        }

        if (!photoModeActive)
        {
            return new InkShowUpdateTransitionPlan(
                ShouldApplySetting: true,
                ShouldReturnAfterSetting: true,
                ShouldClearInkState: false,
                ShouldLoadCurrentPage: false,
                RequestCrossPageUpdateForEnabled: false,
                RequestCrossPageUpdateForDisabled: false);
        }

        if (!nextInkShowEnabled)
        {
            return new InkShowUpdateTransitionPlan(
                ShouldApplySetting: true,
                ShouldReturnAfterSetting: false,
                ShouldClearInkState: true,
                ShouldLoadCurrentPage: false,
                RequestCrossPageUpdateForEnabled: false,
                RequestCrossPageUpdateForDisabled: true);
        }

        return new InkShowUpdateTransitionPlan(
            ShouldApplySetting: true,
            ShouldReturnAfterSetting: false,
            ShouldClearInkState: false,
            ShouldLoadCurrentPage: true,
            RequestCrossPageUpdateForEnabled: true,
            RequestCrossPageUpdateForDisabled: false);
    }
}
