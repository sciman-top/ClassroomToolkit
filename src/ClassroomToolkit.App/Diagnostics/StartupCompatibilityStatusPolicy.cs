using ClassroomToolkit.Services.Compatibility;

namespace ClassroomToolkit.App.Diagnostics;

internal enum CompatibilityHealthStatus
{
    Normal = 0,
    Degraded = 1,
    Blocked = 2
}

internal static class StartupCompatibilityStatusPolicy
{
    public static CompatibilityHealthStatus Resolve(StartupCompatibilityReport? report)
    {
        if (report == null)
        {
            return CompatibilityHealthStatus.Degraded;
        }

        if (report.HasBlockingIssues)
        {
            return CompatibilityHealthStatus.Blocked;
        }

        if (report.HasWarnings)
        {
            return CompatibilityHealthStatus.Degraded;
        }

        return CompatibilityHealthStatus.Normal;
    }

    public static string ToBadgeText(CompatibilityHealthStatus status)
    {
        return status switch
        {
            CompatibilityHealthStatus.Blocked => "兼容状态：阻断",
            CompatibilityHealthStatus.Degraded => "兼容状态：降级",
            _ => "兼容状态：正常"
        };
    }
}
