namespace ClassroomToolkit.App.Paint;

internal static class WpsPresentationRuntimePolicy
{
    internal static bool IsDedicatedSlideshowRuntime(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }

        return processName.StartsWith("wpp", StringComparison.OrdinalIgnoreCase)
               || processName.StartsWith("wppt", StringComparison.OrdinalIgnoreCase)
               || processName.Contains("wpspresentation", StringComparison.OrdinalIgnoreCase);
    }
}
