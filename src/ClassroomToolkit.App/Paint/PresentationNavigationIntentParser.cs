namespace ClassroomToolkit.App.Paint;

internal static class PresentationNavigationIntentParser
{
    internal static bool TryParseHook(
        int direction,
        string? source,
        out PresentationNavigationIntent intent)
    {
        if (direction == 0)
        {
            intent = PresentationNavigationIntent.None;
            return false;
        }

        intent = new PresentationNavigationIntent(
            direction,
            ParseHookSource(source));
        return true;
    }

    private static PresentationNavigationSource ParseHookSource(string? source)
    {
        return string.Equals(source, "wheel", System.StringComparison.OrdinalIgnoreCase)
            ? PresentationNavigationSource.HookWheel
            : PresentationNavigationSource.HookKeyboard;
    }
}
