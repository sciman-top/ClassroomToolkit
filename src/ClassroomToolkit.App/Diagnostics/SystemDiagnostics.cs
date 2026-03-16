using System.IO;
using System.Runtime.InteropServices;
using System.Speech.Synthesis;
using System.Text;
using ClassroomToolkit.App;
using ClassroomToolkit.App.Paint;
using ClassroomToolkit.App.Settings;
using ClassroomToolkit.Services.Presentation;

namespace ClassroomToolkit.App.Diagnostics;

public static class SystemDiagnostics
{
    public static DiagnosticsResult CollectSystemDiagnostics(
        AppSettings settings,
        string settingsPath,
        string studentPath,
        string photoRoot)
    {
        var lines = new List<string>();
        var issues = new List<string>();
        var fixes = new List<string>();

        lines.Add($"平台：{Environment.OSVersion.Platform}");
        lines.Add($"系统：{RuntimeInformation.OSDescription}");
        lines.Add($"架构：{RuntimeInformation.OSArchitecture} 进程={RuntimeInformation.ProcessArchitecture}");
        lines.Add($".NET：{RuntimeInformation.FrameworkDescription}");
        lines.Add($"可执行文件：{Environment.ProcessPath ?? "unknown"}");
        lines.Add($"工作目录：{Environment.CurrentDirectory}");
        lines.Add($"程序目录：{AppDomain.CurrentDomain.BaseDirectory}");

        lines.Add($"设置文件：{settingsPath}");
        if (!TryReportWritable(settingsPath, lines, issues, fixes))
        {
            fixes.Add("请将程序放到可写目录（如用户目录/桌面），或以管理员身份运行。");
        }

        lines.Add($"学生数据文件：{studentPath}");
        if (File.Exists(studentPath))
        {
            lines.Add("学生数据文件：存在");
        }
        else
        {
            lines.Add("学生数据文件：不存在（首次会自动生成模板）");
        }

        lines.Add($"照片根目录：{photoRoot}");
        if (!Directory.Exists(photoRoot))
        {
            lines.Add("照片目录：不存在（首次使用会自动创建）");
        }
        if (TryDetectMultipleSolutionRoots(out var baseRoot, out var currentRoot))
        {
            lines.Add($"检测到多个目录：{baseRoot} / {currentRoot}");
            issues.Add("检测到多个 ClassroomToolkit 目录：学生数据可能来自非当前目录。");
            fixes.Add("请确认当前运行目录与数据目录一致，必要时移动或清理多余副本。");
        }

        lines.Add($"控制Office演示：{(settings.ControlMsPpt ? "启用" : "禁用")}");
        lines.Add($"控制WPS演示：{(settings.ControlWpsPpt ? "启用" : "禁用")}");
        lines.Add($"WPS兼容策略：{settings.WpsInputMode}");
        lines.Add($"WPS滚轮映射：{(settings.WpsWheelForward ? "启用" : "禁用")}");
        lines.Add($"WPS去抖毫秒：{settings.WpsDebounceMs}");
        lines.Add($"降级策略锁定：{(settings.PresentationLockStrategyWhenDegraded ? "启用" : "禁用")}");
        lines.Add($"图片滚轮缩放步进：{settings.PhotoWheelZoomBase:0.####}");
        lines.Add($"图片手势缩放灵敏度：{settings.PhotoGestureZoomSensitivity:0.##}x");
        lines.Add($"跨页抬笔刷新延迟：{settings.PhotoPostInputRefreshDelayMs}ms");
        lines.Add($"图片输入遥测日志：{(settings.PhotoInputTelemetryEnabled ? "启用" : "禁用")}");
        lines.Add($"全屏演示前台保障：{(settings.ForcePresentationForegroundOnFullscreen ? "启用" : "禁用")}");
        var presetRecommendation = PresetSchemePolicy.ResolveRecommendation(settings);
        lines.Add($"当前预设方案：{settings.PresetScheme}");
        lines.Add($"推荐预设方案：{presetRecommendation.Scheme}（{(presetRecommendation.HasAdaptiveSignal ? "基于设备画像" : "默认推荐")}）");
        lines.Add($"推荐依据：{presetRecommendation.Reason}");

        AppendPresentationDiagnostics(lines, issues, fixes, settings);

        if (OperatingSystem.IsWindows())
        {
            var voices = GetVoiceCount(out var voiceError);
            if (voices >= 0)
            {
                lines.Add($"系统语音包数量：{voices}");
            }
            if (!string.IsNullOrWhiteSpace(voiceError))
            {
                lines.Add($"语音包检测错误：{voiceError}");
                issues.Add("语音播报可能不可用：语音包检测异常。");
                fixes.Add("尝试在 Windows“语音/讲述人/语言包”中安装中文语音后重启。");
            }
        }
        else
        {
            issues.Add("当前系统不是 Windows，部分功能不可用。");
            fixes.Add("请在 Windows 10/11 上运行以使用完整功能。");
        }

        var title = "系统兼容性诊断";
        var detail = string.Join(Environment.NewLine, lines);
        var suggestion = BuildSuggestions(issues, fixes, okMessage: "✅ 当前系统环境良好，关键检查通过。");
        return new DiagnosticsResult(issues.Count > 0, title, detail, suggestion);
    }

    public static DiagnosticsResult CollectQuickDiagnostics(string settingsPath)
    {
        var lines = new List<string>();
        var issues = new List<string>();
        var fixes = new List<string>();

        lines.Add($"平台：{Environment.OSVersion.Platform}");
        lines.Add($".NET：{RuntimeInformation.FrameworkDescription}");
        lines.Add($"程序目录：{AppDomain.CurrentDomain.BaseDirectory}");
        lines.Add($"设置文件：{settingsPath}");
        if (!TryReportWritable(settingsPath, lines, issues, fixes))
        {
            fixes.Add("请将程序放到可写目录（如用户目录/桌面），或以管理员身份运行。");
        }
        if (TryDetectMultipleSolutionRoots(out var baseRoot, out var currentRoot))
        {
            lines.Add($"检测到多个目录：{baseRoot} / {currentRoot}");
            issues.Add("检测到多个 ClassroomToolkit 目录：学生数据可能来自非当前目录。");
            fixes.Add("请确认当前运行目录与数据目录一致，必要时移动或清理多余副本。");
        }

        if (!OperatingSystem.IsWindows())
        {
            issues.Add("当前系统不是 Windows，部分功能不可用。");
            fixes.Add("请在 Windows 10/11 上运行以使用完整功能。");
        }

        var title = "启动快速检查";
        var detail = string.Join(Environment.NewLine, lines);
        var suggestion = BuildSuggestions(issues, fixes, okMessage: "✅ 启动快速检查通过。");
        return new DiagnosticsResult(issues.Count > 0, title, detail, suggestion);
    }

    private static bool TryReportWritable(
        string settingsPath,
        ICollection<string> lines,
        ICollection<string> issues,
        ICollection<string> fixes)
    {
        var directory = Path.GetDirectoryName(settingsPath) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = Environment.CurrentDirectory;
        }
        if (!Directory.Exists(directory))
        {
            lines.Add("设置目录：不存在");
            issues.Add("设置目录不存在：配置无法保存。");
            return false;
        }
        var writable = IsWritableDirectory(directory);
        lines.Add($"设置目录可写：{(writable ? "OK" : "否")}");
        if (!writable)
        {
            issues.Add("设置目录不可写：配置无法保存。");
            fixes.Add("请确认当前目录具备写入权限。");
        }
        return writable;
    }

    private static bool IsWritableDirectory(string path)
    {
        try
        {
            var testName = $"ctoolkit_{Guid.NewGuid():N}.tmp";
            var filePath = Path.Combine(path, testName);
            File.WriteAllText(filePath, "probe", Encoding.UTF8);
            File.Delete(filePath);
            return true;
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            return false;
        }
    }

    private static int GetVoiceCount(out string error)
    {
        error = string.Empty;
        try
        {
            using var synth = new SpeechSynthesizer();
            return synth.GetInstalledVoices().Count;
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            error = ex.Message;
            return -1;
        }
    }

    private static string BuildSuggestions(IReadOnlyList<string> issues, IReadOnlyList<string> fixes, string okMessage)
    {
        var lines = new List<string>();
        if (issues.Count == 0)
        {
            lines.Add(okMessage);
            return string.Join(Environment.NewLine, lines);
        }
        lines.AddRange(issues.Select(item => $"问题：{item}"));
        if (fixes.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("解决建议：");
            lines.AddRange(fixes.Select(item => $"· {item}"));
        }
        return string.Join(Environment.NewLine, lines);
    }

    private static void AppendPresentationDiagnostics(
        ICollection<string> lines,
        ICollection<string> issues,
        ICollection<string> fixes,
        AppSettings settings)
    {
        var result = PresentationDiagnosticsProbe.Collect(
            settings.ControlWpsPpt,
            settings.ControlMsPpt,
            (uint)Environment.ProcessId);
        foreach (var line in result.Lines)
        {
            lines.Add(line);
        }
        foreach (var issue in result.Issues)
        {
            issues.Add(issue);
        }
        foreach (var fix in result.Fixes)
        {
            fixes.Add(fix);
        }
    }

    private static bool TryDetectMultipleSolutionRoots(out string? baseRoot, out string? currentRoot)
    {
        baseRoot = FindSolutionDirectory(AppDomain.CurrentDomain.BaseDirectory);
        currentRoot = FindSolutionDirectory(Environment.CurrentDirectory);
        if (string.IsNullOrWhiteSpace(baseRoot) || string.IsNullOrWhiteSpace(currentRoot))
        {
            return false;
        }
        return !string.Equals(baseRoot, currentRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static string? FindSolutionDirectory(string startPath)
    {
        if (string.IsNullOrWhiteSpace(startPath))
        {
            return null;
        }
        var current = new DirectoryInfo(Path.GetFullPath(startPath));
        while (current != null)
        {
            var slnPath = Path.Combine(current.FullName, "ClassroomToolkit.sln");
            if (File.Exists(slnPath))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        return null;
    }
}
