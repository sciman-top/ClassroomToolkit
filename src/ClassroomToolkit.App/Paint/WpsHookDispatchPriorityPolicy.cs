using System.Windows.Threading;

namespace ClassroomToolkit.App.Paint;

internal static class WpsHookDispatchPriorityPolicy
{
    internal static DispatcherPriority Resolve(string? source)
    {
        if (string.Equals(source, "keyboard", System.StringComparison.OrdinalIgnoreCase))
        {
            // Keyboard page-turn should feel immediate in cursor mode.
            return DispatcherPriority.Input;
        }

        return DispatcherPriority.Background;
    }
}
