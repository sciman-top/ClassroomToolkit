namespace ClassroomToolkit.App.Paint;

internal enum RegionSelectionCompletionDecision
{
    KeepWaiting = 0,
    Accept = 1
}

internal static class RegionSelectionCompletionPolicy
{
    internal const double MinimumSelectionSize = 4;

    internal static RegionSelectionCompletionDecision ResolvePointerRelease(double width, double height)
    {
        return width >= MinimumSelectionSize && height >= MinimumSelectionSize
            ? RegionSelectionCompletionDecision.Accept
            : RegionSelectionCompletionDecision.KeepWaiting;
    }
}
