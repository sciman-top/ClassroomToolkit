namespace ClassroomToolkit.App.Paint;

internal static class PresentationNavigationDebounceMsPolicy
{
    internal static int Resolve(int configuredMs, int fallbackMs)
    {
        if (configuredMs >= 0)
        {
            return configuredMs;
        }

        return fallbackMs < 0 ? 0 : fallbackMs;
    }
}
