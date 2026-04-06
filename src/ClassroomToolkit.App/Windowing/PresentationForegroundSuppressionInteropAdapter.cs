using System;

namespace ClassroomToolkit.App.Windowing;

internal static class PresentationForegroundSuppressionInteropAdapter
{
    internal static IDisposable SuppressForeground()
    {
        return PresentationWindowFocus.SuppressForeground();
    }
}
