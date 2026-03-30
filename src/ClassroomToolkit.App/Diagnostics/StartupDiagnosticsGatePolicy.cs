using System;

namespace ClassroomToolkit.App.Diagnostics;

internal static class StartupDiagnosticsGatePolicy
{
    internal static bool ShouldRun(string? disableFlag)
    {
        return !string.Equals(disableFlag?.Trim(), "1", StringComparison.OrdinalIgnoreCase);
    }
}
