namespace ClassroomToolkit.App.Paint;

internal static class PresentationReservedNavigationKeyPolicy
{
    private static readonly IReadOnlyCollection<VirtualKey> Empty = Array.Empty<VirtualKey>();

    internal static IReadOnlyCollection<VirtualKey> ResolveRollCallGroupSwitchKeys(
        bool enabled,
        string? configuredKey)
    {
        if (!enabled)
        {
            return Empty;
        }

        var token = string.IsNullOrWhiteSpace(configuredKey)
            ? "enter"
            : configuredKey.Trim();
        return KeyBindingParser.TryParse(token, out var binding) && binding != null
            ? [binding.Key]
            : Empty;
    }
}
