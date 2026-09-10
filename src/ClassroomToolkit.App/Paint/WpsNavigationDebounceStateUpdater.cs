using System;

namespace ClassroomToolkit.App.Paint;

internal static class WpsNavigationDebounceStateUpdater
{
    internal static void Apply(
        ref (int Code, IntPtr Target, DateTime Timestamp)? lastEvent,
        WpsNavigationDebounceState state)
    {
        lastEvent = state.LastEvent;
    }
}
