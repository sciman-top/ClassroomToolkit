namespace ClassroomToolkit.App.Paint;

internal readonly record struct CrossPageDisplayToggleFlagUpdateDecision(
    bool ShouldApply,
    bool NextCrossPageDisplayEnabled);

internal static class CrossPageDisplayToggleFlagUpdatePolicy
{
    internal static CrossPageDisplayToggleFlagUpdateDecision Resolve(
        bool currentCrossPageDisplayEnabled,
        bool requestedEnabled)
    {
        var unchanged = currentCrossPageDisplayEnabled == requestedEnabled;
        if (unchanged)
        {
            return new CrossPageDisplayToggleFlagUpdateDecision(
                ShouldApply: false,
                NextCrossPageDisplayEnabled: currentCrossPageDisplayEnabled);
        }

        return new CrossPageDisplayToggleFlagUpdateDecision(
            ShouldApply: true,
            NextCrossPageDisplayEnabled: requestedEnabled);
    }
}
