using ClassroomToolkit.Services.Compatibility;

namespace ClassroomToolkit.App.Diagnostics;

internal static class StartupCompatibilitySuppressionPolicy
{
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
}
