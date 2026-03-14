using System;

namespace ClassroomToolkit.App.Paint;

internal static class PresentationRuntimeDefaults
{
    internal const int FocusMonitorIntervalMs = 500;
    internal const int FocusRestoreCooldownMs = 1200;
    internal const int WpsNavDebounceMs = 200;
    internal static readonly DateTime UnsetTimestampUtc = DateTime.MinValue;
}
