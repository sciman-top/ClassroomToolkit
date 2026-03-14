namespace ClassroomToolkit.App.Paint;

internal static class PresentationChannelAvailabilityPolicy
{
    internal static bool IsAnyChannelEnabled(bool allowOffice, bool allowWps)
    {
        return allowOffice || allowWps;
    }
}
