namespace ClassroomToolkit.App.Paint;

internal readonly record struct CrossPageUpdateRequestRuntimeState(
    CrossPageUpdateRequestContext? LastRequest,
    DateTime LastRequestUtc)
{
    internal static CrossPageUpdateRequestRuntimeState Default => new(
        LastRequest: null,
        LastRequestUtc: CrossPageRuntimeDefaults.UnsetTimestampUtc);
}
