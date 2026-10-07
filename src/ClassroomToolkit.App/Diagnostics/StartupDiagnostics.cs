using ClassroomToolkit.Services.Compatibility;
using System;

namespace ClassroomToolkit.App.Diagnostics;

public sealed record DiagnosticsResult(
    bool HasIssues,
    string Title,
    string Detail,
    string Suggestion,
    string HealthBadge)
{
    public string Summary
    {
        get
        {
            if (string.IsNullOrWhiteSpace(HealthBadge))
            {
                return HasIssues
                    ? "检测到潜在兼容问题。"
                    : "系统环境检测正常。";
            }

            return HasIssues
                ? $"{HealthBadge}（检测到潜在兼容问题）"
                : $"{HealthBadge}（系统环境检测正常）";
        }
    }
}

internal enum CompatibilityHealthStatus
{
    Normal = 0,
    Degraded = 1,
    Blocked = 2
}

internal static class StartupDiagnosticsPolicies
{
    internal static bool ShouldRun(string? disableFlag)
    {
        return !string.Equals(disableFlag?.Trim(), "1", StringComparison.OrdinalIgnoreCase);
    }

    internal static StartupCompatibilityReport FilterWarnings(
        StartupCompatibilityReport report,
        IReadOnlyCollection<string>? suppressedCodes)
    {
        ArgumentNullException.ThrowIfNull(report);

        if (suppressedCodes == null || suppressedCodes.Count == 0)
        {
            return report;
        }

        var filtered = report.Issues
            .Where(issue => issue.IsBlocking || !suppressedCodes.Contains(issue.Code, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        return new StartupCompatibilityReport(filtered);
    }

    internal static List<string> MergeSuppressedWarningCodes(
        IReadOnlyCollection<string>? existingCodes,
        StartupCompatibilityReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var merged = new HashSet<string>(existingCodes ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        foreach (var issue in report.Issues)
        {
            if (!issue.IsBlocking && !string.IsNullOrWhiteSpace(issue.Code))
            {
                merged.Add(issue.Code);
            }
        }

        return merged.ToList();
    }

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
