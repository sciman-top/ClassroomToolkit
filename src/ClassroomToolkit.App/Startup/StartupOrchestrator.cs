using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using ClassroomToolkit.App.Diagnostics;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.Settings;
using ClassroomToolkit.Application.Abstractions;
using ClassroomToolkit.Services.Compatibility;
using Microsoft.Extensions.DependencyInjection;

namespace ClassroomToolkit.App.Startup;

internal sealed class StartupOrchestrator
{
    private readonly IServiceProvider _services;
    private readonly string _appDataDirectory;
    private readonly IDictionary _appProperties;
    private readonly string _startupWarningShownPropertyKey;
    private readonly Action<Exception, string> _logException;

    internal StartupOrchestrator(
        IServiceProvider services,
        string appDataDirectory,
        IDictionary appProperties,
        string startupWarningShownPropertyKey,
        Action<Exception, string> logException)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(appDataDirectory);
        ArgumentNullException.ThrowIfNull(appProperties);
        ArgumentException.ThrowIfNullOrWhiteSpace(startupWarningShownPropertyKey);
        ArgumentNullException.ThrowIfNull(logException);

        _services = services;
        _appDataDirectory = appDataDirectory;
        _appProperties = appProperties;
        _startupWarningShownPropertyKey = startupWarningShownPropertyKey;
        _logException = logException;
    }

    internal bool RunCompatibilityGate()
    {
        var settingsPath = ResolveStartupSettingsPath();
        var startupCompatibility = CollectStartupCompatibilityReport(settingsPath);
        var startupCompatibilityReportPath = PersistStartupCompatibilityReport(startupCompatibility);
        var settings = _services.GetService<AppSettings>();
        var autoRemediation = StartupCompatibilityAutoRemediationPolicy.Apply(
            startupCompatibility,
            settings,
            settingsPath);
        if (autoRemediation.HasSettingsChanges && settings != null)
        {
            try
            {
                _services.GetService<AppSettingsService>()?.Save(settings);
                Debug.WriteLine(
                    $"[StartupCompatibility] Auto remediation applied: {string.Join(" | ", autoRemediation.AppliedActions)}");
            }
            catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
            {
                _logException(ex, "StartupCompatibilityAutoRemediationPersist");
            }
        }
        if (startupCompatibility.HasBlockingIssues)
        {
            var message = BuildStartupBlockingMessage(startupCompatibility, startupCompatibilityReportPath);
            System.Windows.MessageBox.Show(
                message,
                "启动环境不兼容",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }

        var visibleWarningReport = StartupCompatibilitySuppressionPolicy.FilterWarnings(
            startupCompatibility,
            settings?.StartupCompatibilitySuppressedIssueCodes);
        if (visibleWarningReport.HasWarnings)
        {
            _appProperties[_startupWarningShownPropertyKey] = true;
            Debug.WriteLine($"[StartupCompatibility] {visibleWarningReport.BuildMessage(includeWarnings: true)}");
            var warningMessage = BuildStartupWarningMessage(
                visibleWarningReport,
                startupCompatibilityReportPath,
                autoRemediation);
            var suggestionMessage = BuildStartupWarningSuggestion(visibleWarningReport);
            var diagnosticsPayload = BuildStartupSupportPayload(
                visibleWarningReport,
                startupCompatibilityReportPath,
                autoRemediation);
            var dialog = new StartupCompatibilityWarningDialog(
                "发现可降级运行风险。",
                warningMessage,
                suggestionMessage,
                startupCompatibilityReportPath,
                diagnosticsPayload);
            _ = dialog.SafeShowDialog();
            if (dialog.SuppressCurrentIssues && settings != null)
            {
                settings.StartupCompatibilitySuppressedIssueCodes =
                    StartupCompatibilitySuppressionPolicy.MergeSuppressedWarningCodes(
                        settings.StartupCompatibilitySuppressedIssueCodes,
                        visibleWarningReport);
                try
                {
                    _services.GetService<AppSettingsService>()?.Save(settings);
                }
                catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
                {
                    _logException(ex, "StartupCompatibilitySuppressionPersist");
                }
            }
        }

        return true;
    }

    private string ResolveStartupSettingsPath()
    {
        var configuration = _services.GetService<IConfigurationService>();
        return configuration?.SettingsDocumentPath
            ?? configuration?.SettingsIniPath
            ?? Path.Combine(_appDataDirectory, "settings.json");
    }

    private StartupCompatibilityReport CollectStartupCompatibilityReport(string settingsPath)
    {
        try
        {
            var settings = _services.GetService<AppSettings>();
            return StartupCompatibilityProbe.Collect(
                settingsPath,
                settings?.PresentationClassifierOverridesJson);
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            _logException(ex, "StartupCompatibilityProbe");
            return new StartupCompatibilityReport(Array.Empty<StartupCompatibilityIssue>());
        }
    }

    private static string BuildStartupBlockingMessage(
        StartupCompatibilityReport report,
        string? reportPath)
    {
        var baseMessage = report.BuildMessage(includeWarnings: false);
        var hasPrivilegeMismatch =
            report.Issues.Any(static issue => issue.Code == "presentation-privilege-mismatch");
        if (!hasPrivilegeMismatch && string.IsNullOrWhiteSpace(reportPath))
        {
            return baseMessage;
        }

        var lines = new List<string> { baseMessage };
        if (hasPrivilegeMismatch)
        {
            lines.Add(string.Empty);
            lines.Add("快速修复：");
            lines.Add("1. 关闭本程序、PPT、WPS。");
            lines.Add("2. 用相同权限重启。");
            lines.Add("3. 若仍失败，先重装标准包。");
        }
        if (!string.IsNullOrWhiteSpace(reportPath))
        {
            lines.Add(string.Empty);
            lines.Add($"诊断报告：{reportPath}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildStartupWarningMessage(
        StartupCompatibilityReport report,
        string? startupCompatibilityReportPath,
        StartupCompatibilityAutoRemediationResult autoRemediation)
    {
        var lines = new List<string>
        {
            "发现可降级运行风险：",
            report.BuildMessage(includeWarnings: true),
            string.Empty
        };
        if (autoRemediation.HasChanges)
        {
            lines.Add("已应用稳定性保护：");
            for (var i = 0; i < autoRemediation.AppliedActions.Count; i++)
            {
                lines.Add($"- {autoRemediation.AppliedActions[i]}");
            }
            lines.Add(string.Empty);
        }
        var quickFixLines = BuildStartupWarningQuickFixLines(report);
        if (quickFixLines.Count > 0)
        {
            lines.Add("快速修复：");
            for (var i = 0; i < quickFixLines.Count; i++)
            {
                lines.Add($"{i + 1}. {quickFixLines[i]}");
            }
            lines.Add(string.Empty);
        }
            lines.Add("程序将继续启动。请尽快修复。");
        if (!string.IsNullOrWhiteSpace(startupCompatibilityReportPath))
        {
            lines.Add($"诊断报告：{startupCompatibilityReportPath}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static IReadOnlyList<string> BuildStartupWarningQuickFixLines(
        StartupCompatibilityReport report)
    {
        var issueCodes = new HashSet<string>(
            report.Issues.Select(issue => issue.Code),
            StringComparer.OrdinalIgnoreCase);
        var lines = new List<string>();

        if (issueCodes.Contains("presentation-arch-mismatch"))
        {
            lines.Add("关闭本程序、PPT、WPS。");
            lines.Add("确保同位数，建议都用 x64。");
            lines.Add("若只装 x86 WPS/Office，请改装 x64 后重启。");
        }
        else if (issueCodes.Contains("presentation-arch-unknown"))
        {
            lines.Add("关闭本程序、PPT、WPS 后重启。");
            lines.Add("临时关闭拦截探测的软件，再次启动复测。");
            lines.Add("优先用 x64 版 Office/WPS，减少波动。");
        }

        return lines;
    }

    private static string BuildStartupWarningSuggestion(StartupCompatibilityReport report)
    {
        return string.Join(
            Environment.NewLine + Environment.NewLine,
            report.Issues
                .Where(static issue => !issue.IsBlocking && !string.IsNullOrWhiteSpace(issue.Suggestion))
                .Select(issue => $"{issue.Code}{Environment.NewLine}{issue.Suggestion}"));
    }

    private static string BuildStartupSupportPayload(
        StartupCompatibilityReport report,
        string? startupCompatibilityReportPath,
        StartupCompatibilityAutoRemediationResult autoRemediation)
    {
        var lines = new List<string>
        {
            "【ClassroomToolkit 启动兼容性诊断】",
            $"时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            "结论：已切到兼容优先模式，可继续上课。",
            string.Empty,
            "风险码："
        };

        foreach (var issue in report.Issues.Where(static issue => !issue.IsBlocking))
        {
            lines.Add($"- {issue.Code}: {issue.Message}");
        }

        if (autoRemediation.AppliedActions.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("已自动执行：");
            for (var i = 0; i < autoRemediation.AppliedActions.Count; i++)
            {
                lines.Add($"- {autoRemediation.AppliedActions[i]}");
            }
        }

        var quickFixLines = BuildStartupWarningQuickFixLines(report);
        if (quickFixLines.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("建议处理：");
            for (var i = 0; i < quickFixLines.Count; i++)
            {
                lines.Add($"{i + 1}. {quickFixLines[i]}");
            }
        }

        if (!string.IsNullOrWhiteSpace(startupCompatibilityReportPath))
        {
            lines.Add(string.Empty);
            lines.Add($"诊断报告：{startupCompatibilityReportPath}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private string? PersistStartupCompatibilityReport(StartupCompatibilityReport report)
    {
        try
        {
            var logPath = Path.Combine(_appDataDirectory, "logs");
            Directory.CreateDirectory(logPath);
            var filePath = Path.Combine(logPath, "startup-compatibility-latest.json");
            File.WriteAllText(filePath, report.ToJson());
            return filePath;
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            _logException(ex, "StartupCompatibilityReportPersist");
            return null;
        }
    }
}
