using ClassroomToolkit.Interop.Presentation;
using System.Diagnostics;

namespace ClassroomToolkit.Services.Presentation;

public readonly record struct PresentationDiagnosticsProbeResult(
    IReadOnlyList<string> Lines,
    IReadOnlyList<string> Issues,
    IReadOnlyList<string> Fixes);

public static class PresentationDiagnosticsProbe
{
    private const int HookStartWaitTimeoutMs = 2000;

    public static PresentationDiagnosticsProbeResult Collect(
        bool allowWps,
        bool allowOffice,
        uint currentProcessId,
        string? classifierOverridesJson = null)
    {
        var lines = new List<string>();
        var issues = new List<string>();
        var fixes = new List<string>();

        if (!OperatingSystem.IsWindows())
        {
            return new PresentationDiagnosticsProbeResult(lines, issues, fixes);
        }

        if (!PresentationClassifierOverridesParser.TryParse(
                classifierOverridesJson,
                out var classifierOverrides,
                out var parseError))
        {
            lines.Add($"分类覆盖解析失败：{parseError}");
            classifierOverrides = PresentationClassifierOverrides.Empty;
        }
        if (!PresentationClassifierOverridesParser.TryParseScoringOptions(
                classifierOverridesJson,
                out var scoringOptions,
                out var scoringError))
        {
            lines.Add($"评分配置解析失败：{scoringError}");
            scoringOptions = PresentationWindowScoringOptions.Default;
        }

        var classifier = new PresentationClassifier(classifierOverrides);
        var resolver = new Win32PresentationResolver();
        resolver.UpdateScoringOptions(scoringOptions);
        lines.Add(
            $"候选评分：class={scoringOptions.ClassMatchWeight}, process={scoringOptions.ProcessMatchWeight}, noCaption={scoringOptions.NoCaptionWeight}, fullscreen={scoringOptions.IsFullscreenWeight}, min={scoringOptions.MinimumCandidateScore}");

        var foreground = resolver.ResolveForeground();
        if (foreground.IsValid)
        {
            lines.Add($"前台窗口进程：{foreground.Info.ProcessName}");
            lines.Add($"前台窗口类名：{FormatClassNames(foreground.Info.ClassNames)}");
            lines.Add(BuildProcessVersionDiagnosticLine("前台窗口版本", foreground.Info.ProcessId));
            var check = resolver.CheckWindow(foreground.Handle, classifier);
            if (check != null)
            {
                lines.Add($"前台窗口判定：{check.Type} 评分={check.Score}");
                lines.Add($"前台窗口全屏：{(check.IsFullscreen ? "是" : "否")} 标题栏：{(check.HasCaption ? "有" : "无")}");
            }
            else
            {
                lines.Add("前台窗口判定：非放映窗口或可信度不足");
            }
        }
        else
        {
            lines.Add("前台窗口：无法获取");
        }

        var target = resolver.ResolvePresentationTarget(
            classifier,
            allowWps,
            allowOffice,
            currentProcessId);
        if (target.IsValid)
        {
            var check = resolver.CheckWindow(target.Handle, classifier);
            lines.Add($"检测到演示窗口：{target.Info.ProcessName}");
            lines.Add($"演示窗口类名：{FormatClassNames(target.Info.ClassNames)}");
            lines.Add(BuildProcessVersionDiagnosticLine("演示窗口版本", target.Info.ProcessId));
            if (check != null)
            {
                lines.Add($"演示窗口判定：{check.Type} 评分={check.Score}");
                lines.Add($"演示窗口全屏：{(check.IsFullscreen ? "是" : "否")} 标题栏：{(check.HasCaption ? "有" : "无")}");
            }
        }
        else
        {
            lines.Add("检测到演示窗口：未找到");
        }

        var hookAvailable = TryCheckWpsHook(out var hookError);
        lines.Add($"WPS全局钩子：{(hookAvailable ? "可用" : "不可用")}");
        if (!hookAvailable)
        {
            if (!string.IsNullOrWhiteSpace(hookError))
            {
                lines.Add($"WPS钩子错误：{hookError}");
            }
            issues.Add("WPS 全局钩子不可用：已自动降级为消息投递模式。");
            fixes.Add("可尝试以管理员身份运行，或检查安全软件是否拦截全局钩子。");
        }

        var remoteHookAvailable = TryCheckRemoteHook(out var remoteHookError);
        lines.Add($"遥控点名钩子：{(remoteHookAvailable ? "可用" : "不可用")}");
        if (!remoteHookAvailable)
        {
            if (!string.IsNullOrWhiteSpace(remoteHookError))
            {
                lines.Add($"遥控钩子错误：{remoteHookError}");
            }
            issues.Add("遥控点名钩子不可用：翻页笔快捷键可能无法工作。");
            fixes.Add("可尝试以管理员身份运行，或检查安全软件是否拦截全局钩子。");
        }

        return new PresentationDiagnosticsProbeResult(lines, issues, fixes);
    }

    public static bool TrySummarizeClassifierOverrides(
        string? classifierOverridesJson,
        out int classTokenCount,
        out int processTokenCount,
        out string error)
    {
        classTokenCount = 0;
        processTokenCount = 0;
        error = string.Empty;

        if (!PresentationClassifierOverridesParser.TryParse(
                classifierOverridesJson,
                out var overrides,
                out var parseError))
        {
            error = parseError;
            return false;
        }

        classTokenCount =
            overrides.AdditionalWpsClassTokens.Count
            + overrides.AdditionalOfficeClassTokens.Count
            + overrides.AdditionalSlideshowClassTokens.Count;
        processTokenCount =
            overrides.AdditionalWpsProcessTokens.Count
            + overrides.AdditionalOfficeProcessTokens.Count;
        return true;
    }

    internal static string BuildProcessVersionDiagnosticLine(
        string label,
        uint processId)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            label = "进程版本";
        }

        if (!TryGetProcessVersionInfo(
                processId,
                out var fileVersion,
                out var productVersion,
                out var error))
        {
            return $"{label}：无法读取（{error}）";
        }

        var normalizedFileVersion = string.IsNullOrWhiteSpace(fileVersion) ? "未知" : fileVersion;
        var normalizedProductVersion = string.IsNullOrWhiteSpace(productVersion) ? "未知" : productVersion;
        return $"{label}：File={normalizedFileVersion}; Product={normalizedProductVersion}";
    }

    private static bool TryGetProcessVersionInfo(
        uint processId,
        out string fileVersion,
        out string productVersion,
        out string error)
    {
        fileVersion = string.Empty;
        productVersion = string.Empty;
        error = string.Empty;

        if (processId == 0)
        {
            error = "invalid-process-id";
            return false;
        }

        try
        {
            using var process = Process.GetProcessById((int)processId);
            var modulePath = process.MainModule?.FileName;
            if (string.IsNullOrWhiteSpace(modulePath))
            {
                error = "main-module-unavailable";
                return false;
            }

            var versionInfo = FileVersionInfo.GetVersionInfo(modulePath);
            fileVersion = versionInfo.FileVersion ?? string.Empty;
            productVersion = versionInfo.ProductVersion ?? string.Empty;
            return true;
        }
        catch (Exception ex) when (PresentationExceptionFilterPolicy.IsNonFatal(ex))
        {
            error = ex.GetType().Name;
            return false;
        }
    }

    private static bool TryCheckWpsHook(out string error)
    {
        error = string.Empty;
        try
        {
            using var hook = new WpsSlideshowNavigationHook();
            try
            {
                if (!hook.Available)
                {
                    return false;
                }

                if (!TryWaitTask(hook.StartAsync(), HookStartWaitTimeoutMs, out var started, out error))
                {
                    return false;
                }
                if (!started && hook.LastError != 0)
                {
                    error = $"Win32 Error {hook.LastError}";
                }

                return started;
            }
            finally
            {
                hook.Stop();
            }
        }
        catch (Exception ex) when (PresentationExceptionFilterPolicy.IsNonFatal(ex))
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool TryCheckRemoteHook(out string error)
    {
        error = string.Empty;
        try
        {
            using var hook = new KeyboardHook();
            try
            {
                if (!TryWaitTask(hook.StartAsync(), HookStartWaitTimeoutMs, out error))
                {
                    return false;
                }
                var active = hook.IsActive;
                if (!active && hook.LastError != 0)
                {
                    error = $"Win32 Error {hook.LastError}";
                }

                return active;
            }
            finally
            {
                hook.Stop();
            }
        }
        catch (Exception ex) when (PresentationExceptionFilterPolicy.IsNonFatal(ex))
        {
            error = ex.Message;
            return false;
        }
    }

    private static string FormatClassNames(IReadOnlyList<string> names)
    {
        if (names == null || names.Count == 0)
        {
            return "（空）";
        }

        return string.Join(" | ", names.Where(name => !string.IsNullOrWhiteSpace(name)));
    }

    private static bool TryWaitTask<T>(
        Task<T> task,
        int timeoutMs,
        out T result,
        out string error)
    {
        ArgumentNullException.ThrowIfNull(task);
        result = default!;
        error = string.Empty;
        try
        {
            result = task
                .WaitAsync(TimeSpan.FromMilliseconds(timeoutMs))
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
            return true;
        }
        catch (TimeoutException)
        {
            error = "startup timeout";
            return false;
        }
        catch (Exception ex) when (PresentationExceptionFilterPolicy.IsNonFatal(ex))
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool TryWaitTask(
        Task task,
        int timeoutMs,
        out string error)
    {
        ArgumentNullException.ThrowIfNull(task);
        error = string.Empty;
        try
        {
            task
                .WaitAsync(TimeSpan.FromMilliseconds(timeoutMs))
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
            return true;
        }
        catch (TimeoutException)
        {
            error = "startup timeout";
            return false;
        }
        catch (Exception ex) when (PresentationExceptionFilterPolicy.IsNonFatal(ex))
        {
            error = ex.Message;
            return false;
        }
    }
}
