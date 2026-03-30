using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.Json;

namespace ClassroomToolkit.Services.Compatibility;

public sealed record StartupCompatibilityIssue(
    string Code,
    string Message,
    string Suggestion,
    bool IsBlocking);

public sealed class StartupCompatibilityReport
{
    public StartupCompatibilityReport(IReadOnlyList<StartupCompatibilityIssue> issues)
    {
        Issues = issues ?? throw new ArgumentNullException(nameof(issues));
    }

    public IReadOnlyList<StartupCompatibilityIssue> Issues { get; }

    public bool HasBlockingIssues => Issues.Any(static issue => issue.IsBlocking);

    public bool HasWarnings => Issues.Any(static issue => !issue.IsBlocking);

    public string ToJson(bool indented = true)
    {
        var payload = new
        {
            generatedAtUtc = DateTimeOffset.UtcNow,
            environment = new
            {
                osDescription = RuntimeInformation.OSDescription,
                osArchitecture = RuntimeInformation.OSArchitecture.ToString(),
                processArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
                dotnetRuntime = Environment.Version.ToString(),
                baseDirectory = AppContext.BaseDirectory
            },
            hasBlockingIssues = HasBlockingIssues,
            hasWarnings = HasWarnings,
            issues = Issues.Select(issue => new
            {
                code = issue.Code,
                message = issue.Message,
                suggestion = issue.Suggestion,
                isBlocking = issue.IsBlocking
            })
        };

        return JsonSerializer.Serialize(
            payload,
            new JsonSerializerOptions
            {
                WriteIndented = indented
            });
    }

    public string BuildMessage(bool includeWarnings)
    {
        var lines = new List<string>();
        var filtered = Issues
            .Where(issue => includeWarnings || issue.IsBlocking)
            .ToArray();
        if (filtered.Length == 0)
        {
            lines.Add("环境检查通过。");
            return string.Join(Environment.NewLine, lines);
        }

        for (var i = 0; i < filtered.Length; i++)
        {
            var issue = filtered[i];
            lines.Add(
                $"{i + 1}. [{(issue.IsBlocking ? "阻断" : "提示")}] {issue.Message}");
            if (!string.IsNullOrWhiteSpace(issue.Suggestion))
            {
                lines.Add($"   建议：{issue.Suggestion}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }
}

public static class StartupCompatibilityProbe
{
    private const int MinSupportedWindowsBuild = 19045; // Win10 22H2
    private const int ProcessQueryLimitedInformation = 0x1000;
    private const uint TokenQuery = 0x0008;

    public static StartupCompatibilityReport Collect(string settingsPath)
    {
        var issues = new List<StartupCompatibilityIssue>();

        EvaluatePlatform(issues);
        EvaluateSettingsPath(settingsPath, issues);
        EvaluateNativeDependencies(issues);
        EvaluatePresentationPrivilegeConsistency(issues);

        return new StartupCompatibilityReport(issues);
    }

    private static void EvaluatePlatform(ICollection<StartupCompatibilityIssue> issues)
    {
        if (!OperatingSystem.IsWindows())
        {
            issues.Add(new StartupCompatibilityIssue(
                Code: "os-not-windows",
                Message: "当前系统不是 Windows，程序无法保证兼容。",
                Suggestion: "请在 Windows 10 22H2+ 或 Windows 11 22H2+ 上运行。",
                IsBlocking: true));
            return;
        }

        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, MinSupportedWindowsBuild))
        {
            issues.Add(new StartupCompatibilityIssue(
                Code: "os-version-unsupported",
                Message: $"当前 Windows 版本低于受支持基线（Build {MinSupportedWindowsBuild}）。",
                Suggestion: "请升级系统到 Win10 22H2+ 或 Win11 22H2+。",
                IsBlocking: true));
        }

        if (RuntimeInformation.OSArchitecture != Architecture.X64
            || RuntimeInformation.ProcessArchitecture != Architecture.X64)
        {
            issues.Add(new StartupCompatibilityIssue(
                Code: "arch-unsupported",
                Message:
                $"当前架构不受支持（OS={RuntimeInformation.OSArchitecture}, Process={RuntimeInformation.ProcessArchitecture}）。",
                Suggestion: "请使用 x64 系统并部署 win-x64 包。",
                IsBlocking: true));
        }

        if (Environment.Version.Major < 10)
        {
            issues.Add(new StartupCompatibilityIssue(
                Code: "runtime-major-mismatch",
                Message: $"当前 .NET 运行时版本过低（{Environment.Version}）。",
                Suggestion: "请安装 .NET Desktop Runtime 10.x，或使用离线版。",
                IsBlocking: true));
        }
    }

    private static void EvaluateSettingsPath(
        string settingsPath,
        ICollection<StartupCompatibilityIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(settingsPath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(settingsPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        if (!Directory.Exists(directory))
        {
            issues.Add(new StartupCompatibilityIssue(
                Code: "settings-dir-missing",
                Message: $"设置目录不存在：{directory}",
                Suggestion: "请检查部署目录权限并重新初始化配置路径。",
                IsBlocking: false));
            return;
        }
    }

    private static void EvaluateNativeDependencies(ICollection<StartupCompatibilityIssue> issues)
    {
        var baseDirectory = AppContext.BaseDirectory;
        if (string.IsNullOrWhiteSpace(baseDirectory) || !Directory.Exists(baseDirectory))
        {
            return;
        }

        var sqlitePath = Path.Combine(baseDirectory, "e_sqlite3.dll");
        if (!File.Exists(sqlitePath))
        {
            issues.Add(new StartupCompatibilityIssue(
                Code: "native-sqlite-missing",
                Message: $"缺少本地依赖：{sqlitePath}",
                Suggestion: "请重新解压完整发布包，避免手动删减 DLL；必要时重新执行安装。",
                IsBlocking: false));
        }

        var pdfiumCandidates = new[]
        {
            Path.Combine(baseDirectory, "x64", "pdfium.dll"),
            Path.Combine(baseDirectory, "pdfium.dll")
        };
        if (!pdfiumCandidates.Any(File.Exists))
        {
            issues.Add(new StartupCompatibilityIssue(
                Code: "native-pdfium-missing",
                Message: $"缺少 PDF 渲染依赖：{string.Join(" / ", pdfiumCandidates)}",
                Suggestion: "请确认安装目录完整，或改用离线标准包重新部署。",
                IsBlocking: false));
        }
    }

    private static void EvaluatePresentationPrivilegeConsistency(
        ICollection<StartupCompatibilityIssue> issues)
    {
        if (!TryGetCurrentProcessElevation(out var currentElevated, out var currentError))
        {
            issues.Add(new StartupCompatibilityIssue(
                Code: "current-elevation-unknown",
                Message: $"无法确定当前进程权限级别：{currentError}",
                Suggestion: "建议关闭后以明确权限级别（管理员或非管理员）重新启动。",
                IsBlocking: false));
            return;
        }

        var mismatchedProcesses = new List<string>();
        var unknownProcesses = new List<string>();
        foreach (var process in EnumeratePresentationProcesses())
        {
            try
            {
                if (!TryGetProcessElevation(process.Id, out var processElevated, out var error))
                {
                    unknownProcesses.Add($"{process.ProcessName}({process.Id}): {error}");
                    continue;
                }

                if (processElevated != currentElevated)
                {
                    mismatchedProcesses.Add($"{process.ProcessName}({process.Id})");
                }
            }
            finally
            {
                process.Dispose();
            }
        }

        if (mismatchedProcesses.Count > 0)
        {
            issues.Add(new StartupCompatibilityIssue(
                Code: "presentation-privilege-mismatch",
                Message:
                $"检测到 PPT/WPS 与当前程序权限级别不一致：{string.Join(", ", mismatchedProcesses)}",
                Suggestion:
                "请关闭程序与 PPT/WPS 后，以相同权限级别重启（都管理员或都非管理员）。",
                IsBlocking: true));
        }

        if (unknownProcesses.Count > 0)
        {
            issues.Add(new StartupCompatibilityIssue(
                Code: "presentation-privilege-unknown",
                Message:
                $"部分 PPT/WPS 进程权限级别无法探测：{string.Join("; ", unknownProcesses)}",
                Suggestion: "若翻页控制异常，请优先统一权限级别并检查安全软件拦截。",
                IsBlocking: false));
        }
    }

    private static bool TryGetCurrentProcessElevation(out bool elevated, out string error)
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            if (identity == null)
            {
                elevated = false;
                error = "identity-unavailable";
                return false;
            }

            var principal = new WindowsPrincipal(identity);
            elevated = principal.IsInRole(WindowsBuiltInRole.Administrator);
            error = string.Empty;
            return true;
        }
        catch (Exception ex) when (IsNonFatal(ex))
        {
            elevated = false;
            error = ex.Message;
            return false;
        }
    }

    private static IEnumerable<Process> EnumeratePresentationProcesses()
    {
        var acceptedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "POWERPNT",
            "WPP",
            "WPPT"
        };

        Process[] processes;
        try
        {
            processes = Process.GetProcesses();
        }
        catch (Exception ex) when (IsNonFatal(ex))
        {
            Debug.WriteLine($"[StartupCompatibility] Enumerate processes failed: {ex.Message}");
            yield break;
        }

        foreach (var process in processes)
        {
            if (acceptedNames.Contains(process.ProcessName))
            {
                yield return process;
            }
            else
            {
                process.Dispose();
            }
        }
    }

    private static bool TryGetProcessElevation(int processId, out bool elevated, out string error)
    {
        elevated = false;
        error = string.Empty;

        IntPtr processHandle = IntPtr.Zero;
        IntPtr tokenHandle = IntPtr.Zero;
        try
        {
            processHandle = OpenProcess(ProcessQueryLimitedInformation, false, processId);
            if (processHandle == IntPtr.Zero)
            {
                error = $"open-process-failed:{Marshal.GetLastWin32Error()}";
                return false;
            }

            if (!OpenProcessToken(processHandle, TokenQuery, out tokenHandle))
            {
                error = $"open-token-failed:{Marshal.GetLastWin32Error()}";
                return false;
            }

            if (!GetTokenInformation(
                    tokenHandle,
                    TokenInformationClass.TokenElevation,
                    out var tokenElevation,
                    Marshal.SizeOf<TokenElevation>(),
                    out _))
            {
                error = $"get-token-info-failed:{Marshal.GetLastWin32Error()}";
                return false;
            }

            elevated = tokenElevation.TokenIsElevated != 0;
            return true;
        }
        catch (Exception ex) when (IsNonFatal(ex))
        {
            error = ex.Message;
            return false;
        }
        finally
        {
            if (tokenHandle != IntPtr.Zero)
            {
                _ = CloseHandle(tokenHandle);
            }
            if (processHandle != IntPtr.Zero)
            {
                _ = CloseHandle(processHandle);
            }
        }
    }

    private static bool IsNonFatal(Exception ex)
    {
        return ex is not (
            OutOfMemoryException
            or AppDomainUnloadedException
            or BadImageFormatException
            or CannotUnloadAppDomainException
            or InvalidProgramException
            or StackOverflowException
            or AccessViolationException);
    }

    private enum TokenInformationClass
    {
        TokenElevation = 20
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TokenElevation
    {
        public int TokenIsElevated;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(int processAccess, bool inheritHandle, int processId);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(
        IntPtr processHandle,
        uint desiredAccess,
        out IntPtr tokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetTokenInformation(
        IntPtr tokenHandle,
        TokenInformationClass tokenInformationClass,
        out TokenElevation tokenInformation,
        int tokenInformationLength,
        out int returnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}
