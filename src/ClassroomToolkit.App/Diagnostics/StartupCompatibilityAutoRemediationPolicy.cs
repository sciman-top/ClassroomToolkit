using ClassroomToolkit.App.Settings;
using ClassroomToolkit.Services.Compatibility;
using System.IO;

namespace ClassroomToolkit.App.Diagnostics;

internal sealed record StartupCompatibilityAutoRemediationResult(
    bool HasChanges,
    bool HasSettingsChanges,
    IReadOnlyList<string> AppliedActions);

internal static class StartupCompatibilityAutoRemediationPolicy
{
    private static readonly string[] PresentationConservativeModeIssueCodes =
    {
        "presentation-arch-mismatch",
        "presentation-arch-unknown",
        "presentation-privilege-unknown"
    };

    internal static StartupCompatibilityAutoRemediationResult Apply(
        StartupCompatibilityReport report,
        AppSettings? settings,
        string? settingsPath)
    {
        ArgumentNullException.ThrowIfNull(report);

        var issueCodes = new HashSet<string>(
            report.Issues.Select(issue => issue.Code),
            StringComparer.OrdinalIgnoreCase);
        var appliedActions = new List<string>();
        var hasSettingsChanges = false;

        if (TryEnsureSettingsDirectory(issueCodes, settingsPath, out var createdDirectory))
        {
            appliedActions.Add($"已自动创建设置目录：{createdDirectory}");
        }

        if (settings != null && ShouldEnforceConservativePresentationMode(issueCodes))
        {
            if (!string.Equals(
                    settings.OfficeInputMode,
                    WpsInputModeDefaults.Message,
                    StringComparison.OrdinalIgnoreCase))
            {
                settings.OfficeInputMode = WpsInputModeDefaults.Message;
                appliedActions.Add("已将 Office 演示控制策略切换为兼容优先（PostMessage）。");
                hasSettingsChanges = true;
            }

            if (!string.Equals(
                    settings.WpsInputMode,
                    WpsInputModeDefaults.Message,
                    StringComparison.OrdinalIgnoreCase))
            {
                settings.WpsInputMode = WpsInputModeDefaults.Message;
                appliedActions.Add("已将 WPS 演示控制策略切换为兼容优先（PostMessage）。");
                hasSettingsChanges = true;
            }

            if (!settings.PresentationLockStrategyWhenDegraded)
            {
                settings.PresentationLockStrategyWhenDegraded = true;
                appliedActions.Add("已启用“降级后固定兼容模式”以避免课堂中策略抖动。");
                hasSettingsChanges = true;
            }
        }

        return new StartupCompatibilityAutoRemediationResult(
            appliedActions.Count > 0,
            hasSettingsChanges,
            appliedActions);
    }

    private static bool ShouldEnforceConservativePresentationMode(ISet<string> issueCodes)
    {
        for (var i = 0; i < PresentationConservativeModeIssueCodes.Length; i++)
        {
            if (issueCodes.Contains(PresentationConservativeModeIssueCodes[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryEnsureSettingsDirectory(
        ISet<string> issueCodes,
        string? settingsPath,
        out string createdDirectory)
    {
        createdDirectory = string.Empty;

        if (!issueCodes.Contains("settings-dir-missing"))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(settingsPath))
        {
            return false;
        }

        var settingsDirectory = Path.GetDirectoryName(settingsPath);
        if (string.IsNullOrWhiteSpace(settingsDirectory) || Directory.Exists(settingsDirectory))
        {
            return false;
        }

        try
        {
            Directory.CreateDirectory(settingsDirectory);
            createdDirectory = settingsDirectory;
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
