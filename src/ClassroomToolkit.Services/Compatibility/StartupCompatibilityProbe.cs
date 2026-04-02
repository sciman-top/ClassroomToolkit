using System.Diagnostics;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.Json;
using ClassroomToolkit.Interop.Presentation;

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
    private const ushort ImageFileMachineUnknown = 0x0000;
    private const ushort ImageFileMachineI386 = 0x014c;
    private const ushort ImageFileMachineAmd64 = 0x8664;
    private const ushort ImageFileMachineArm64 = 0xAA64;
    private const string VcppRuntimeRegistryPath = @"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64";
    private static readonly string[] DefaultPresentationProcessTokens =
    {
        "powerpnt",
        "wpp",
        "wppt"
    };

    public static StartupCompatibilityReport Collect(
        string settingsPath,
        string? presentationClassifierOverridesJson = null)
    {
        var issues = new List<StartupCompatibilityIssue>();

        EvaluatePlatform(issues);
        EvaluateSettingsPath(settingsPath, issues);
        EvaluateVcppRuntime(issues);
        EvaluateNativeDependencies(issues);
        EvaluatePresentationPrivilegeConsistency(
            issues,
            BuildPresentationProcessTokens(presentationClassifierOverridesJson));
        EvaluatePresentationArchitectureConsistency(
            issues,
            BuildPresentationProcessTokens(presentationClassifierOverridesJson));

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
        else if (TryProbeNativeLibraryLoad(sqlitePath, out var sqliteLoadError))
        {
            issues.Add(new StartupCompatibilityIssue(
                Code: "native-sqlite-load-failed",
                Message: $"本地依赖加载失败：{sqlitePath}（{sqliteLoadError}）",
                Suggestion: "请安装/修复 VC++ 运行库（x64），并确认杀毒软件未拦截本地 DLL。",
                IsBlocking: false));
        }

        var pdfiumCandidates = new[]
        {
            Path.Combine(baseDirectory, "x64", "pdfium.dll"),
            Path.Combine(baseDirectory, "pdfium.dll")
        };
        var pdfiumPath = pdfiumCandidates.FirstOrDefault(File.Exists);
        if (string.IsNullOrWhiteSpace(pdfiumPath))
        {
            issues.Add(new StartupCompatibilityIssue(
                Code: "native-pdfium-missing",
                Message: $"缺少 PDF 渲染依赖：{string.Join(" / ", pdfiumCandidates)}",
                Suggestion: "请确认安装目录完整，或改用离线标准包重新部署。",
                IsBlocking: false));
        }
        else if (TryProbeNativeLibraryLoad(pdfiumPath, out var pdfiumLoadError))
        {
            issues.Add(new StartupCompatibilityIssue(
                Code: "native-pdfium-load-failed",
                Message: $"PDF 渲染依赖加载失败：{pdfiumPath}（{pdfiumLoadError}）",
                Suggestion: "请安装/修复 VC++ 运行库（x64），并检查 pdfium 相关文件是否被安全软件隔离。",
                IsBlocking: false));
        }
    }

    private static void EvaluateVcppRuntime(ICollection<StartupCompatibilityIssue> issues)
    {
        if (!TryGetVcppRuntimeVersion(out var version, out var error))
        {
            issues.Add(new StartupCompatibilityIssue(
                Code: "vcpp-runtime-unknown",
                Message: $"无法确认 VC++ 运行库状态：{error}",
                Suggestion: "建议安装/修复 Visual C++ Redistributable 2015-2022（x64）。",
                IsBlocking: false));
            return;
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            issues.Add(new StartupCompatibilityIssue(
                Code: "vcpp-runtime-missing",
                Message: "未检测到 Visual C++ Redistributable 2015-2022（x64）。",
                Suggestion: "请安装/修复 VC++ 2015-2022 x64 运行库后重试。",
                IsBlocking: false));
        }
    }

    internal static bool TryProbeNativeLibraryLoad(string libraryPath, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(libraryPath))
        {
            error = "path-empty";
            return true;
        }

        IntPtr handle = IntPtr.Zero;
        try
        {
            if (NativeLibrary.TryLoad(libraryPath, out handle))
            {
                return false;
            }

            error = $"load-failed:{Marshal.GetLastPInvokeError()}";
            return true;
        }
        catch (Exception ex) when (IsNonFatal(ex))
        {
            error = ex.Message;
            return true;
        }
        finally
        {
            if (handle != IntPtr.Zero)
            {
                NativeLibrary.Free(handle);
            }
        }
    }

    private static void EvaluatePresentationPrivilegeConsistency(
        ICollection<StartupCompatibilityIssue> issues,
        IReadOnlyList<string> processTokens)
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
        foreach (var process in EnumeratePresentationProcesses(processTokens))
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

    private static void EvaluatePresentationArchitectureConsistency(
        ICollection<StartupCompatibilityIssue> issues,
        IReadOnlyList<string> processTokens)
    {
        var appArch = RuntimeInformation.ProcessArchitecture;
        var mismatchedProcesses = new List<string>();
        var unknownProcesses = new List<string>();
        foreach (var process in EnumeratePresentationProcesses(processTokens))
        {
            try
            {
                if (!TryGetProcessArchitecture(process.Id, out var processArch, out var error))
                {
                    unknownProcesses.Add($"{process.ProcessName}({process.Id}): {error}");
                    continue;
                }

                if (processArch != appArch)
                {
                    mismatchedProcesses.Add($"{process.ProcessName}({process.Id})={processArch}");
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
                Code: "presentation-arch-mismatch",
                Message:
                $"检测到 PPT/WPS 与程序位数可能不一致（App={appArch}）：{string.Join(", ", mismatchedProcesses)}",
                Suggestion:
                "建议统一为 x64（程序与 Office/WPS 同位数），否则翻页控制可能降级或不稳定。",
                IsBlocking: false));
        }

        if (unknownProcesses.Count > 0)
        {
            issues.Add(new StartupCompatibilityIssue(
                Code: "presentation-arch-unknown",
                Message:
                $"部分 PPT/WPS 进程位数无法探测：{string.Join("; ", unknownProcesses)}",
                Suggestion: "若翻页控制异常，请检查安全软件拦截并优先使用 x64 版本。",
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
        return EnumeratePresentationProcesses(DefaultPresentationProcessTokens);
    }

    private static IEnumerable<Process> EnumeratePresentationProcesses(
        IReadOnlyList<string> processTokens)
    {
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
            if (IsPresentationProcessNameMatch(process.ProcessName, processTokens))
            {
                yield return process;
            }
            else
            {
                process.Dispose();
            }
        }
    }

    internal static IReadOnlyList<string> BuildPresentationProcessTokens(
        string? presentationClassifierOverridesJson)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddProcessTokens(tokens, DefaultPresentationProcessTokens);

        if (!PresentationClassifierOverridesParser.TryParse(
                presentationClassifierOverridesJson,
                out var overrides,
                out _))
        {
            return tokens.ToArray();
        }

        AddProcessTokens(tokens, overrides.AdditionalWpsProcessTokens);
        AddProcessTokens(tokens, overrides.AdditionalOfficeProcessTokens);
        return tokens.ToArray();
    }

    internal static bool IsPresentationProcessNameMatch(
        string processName,
        IReadOnlyList<string> processTokens)
    {
        if (string.IsNullOrWhiteSpace(processName)
            || processTokens == null
            || processTokens.Count == 0)
        {
            return false;
        }

        for (var i = 0; i < processTokens.Count; i++)
        {
            var token = processTokens[i];
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            if (processName.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void AddProcessTokens(ISet<string> target, IReadOnlyList<string> source)
    {
        if (source == null || source.Count == 0)
        {
            return;
        }

        for (var i = 0; i < source.Count; i++)
        {
            var normalized = NormalizeProcessToken(source[i]);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }

            target.Add(normalized);
        }
    }

    private static string NormalizeProcessToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim();
        if (normalized.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^4];
        }

        return normalized;
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

    internal static bool TryGetVcppRuntimeVersion(out string version, out string error)
    {
        version = string.Empty;
        error = string.Empty;

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(VcppRuntimeRegistryPath);
            if (key == null)
            {
                return true;
            }

            var installed = ToInt32OrDefault(key.GetValue("Installed"));
            if (installed != 1)
            {
                return true;
            }

            var major = ToInt32OrDefault(key.GetValue("Major"));
            var minor = ToInt32OrDefault(key.GetValue("Minor"));
            var bld = ToInt32OrDefault(key.GetValue("Bld"));
            version = $"{major}.{minor}.{bld}";
            return true;
        }
        catch (Exception ex) when (IsNonFatal(ex))
        {
            error = ex.Message;
            return false;
        }
    }

    internal static bool TryGetProcessArchitecture(
        int processId,
        out Architecture architecture,
        out string error)
    {
        architecture = Architecture.X64;
        error = string.Empty;

        IntPtr processHandle = IntPtr.Zero;
        try
        {
            processHandle = OpenProcess(ProcessQueryLimitedInformation, false, processId);
            if (processHandle == IntPtr.Zero)
            {
                error = $"open-process-failed:{Marshal.GetLastWin32Error()}";
                return false;
            }

            if (!TryDetectProcessMachine(processHandle, out var processMachine, out error))
            {
                return false;
            }

            architecture = MapMachineToArchitecture(processMachine);
            return true;
        }
        catch (Exception ex) when (IsNonFatal(ex))
        {
            error = ex.Message;
            return false;
        }
        finally
        {
            if (processHandle != IntPtr.Zero)
            {
                _ = CloseHandle(processHandle);
            }
        }
    }

    internal static Architecture MapMachineToArchitecture(ushort machine)
    {
        return machine switch
        {
            ImageFileMachineI386 => Architecture.X86,
            ImageFileMachineAmd64 => Architecture.X64,
            ImageFileMachineArm64 => Architecture.Arm64,
            _ => RuntimeInformation.OSArchitecture
        };
    }

    private static bool TryDetectProcessMachine(
        IntPtr processHandle,
        out ushort processMachine,
        out string error)
    {
        processMachine = ImageFileMachineUnknown;
        error = string.Empty;

        if (processHandle == IntPtr.Zero)
        {
            error = "invalid-handle";
            return false;
        }

        if (TryGetMachineFromWow64Process2(processHandle, out processMachine, out var wow64v2Error))
        {
            return true;
        }

        if (!string.Equals(wow64v2Error, "entry-point-not-found", StringComparison.OrdinalIgnoreCase))
        {
            error = wow64v2Error;
            return false;
        }

        if (!TryGetMachineFromWow64Process(processHandle, out processMachine, out error))
        {
            return false;
        }

        return true;
    }

    private static bool TryGetMachineFromWow64Process2(
        IntPtr processHandle,
        out ushort processMachine,
        out string error)
    {
        processMachine = ImageFileMachineUnknown;
        error = string.Empty;

        try
        {
            if (!IsWow64Process2(processHandle, out processMachine, out var nativeMachine))
            {
                error = $"iswow64process2-failed:{Marshal.GetLastWin32Error()}";
                return false;
            }

            if (processMachine == ImageFileMachineUnknown)
            {
                processMachine = nativeMachine == ImageFileMachineUnknown
                    ? ImageFileMachineAmd64
                    : nativeMachine;
            }
            return true;
        }
        catch (EntryPointNotFoundException)
        {
            error = "entry-point-not-found";
            return false;
        }
    }

    private static bool TryGetMachineFromWow64Process(
        IntPtr processHandle,
        out ushort processMachine,
        out string error)
    {
        processMachine = ImageFileMachineUnknown;
        error = string.Empty;

        if (!IsWow64Process(processHandle, out var isWow64))
        {
            error = $"iswow64process-failed:{Marshal.GetLastWin32Error()}";
            return false;
        }

        processMachine = isWow64 ? ImageFileMachineI386 : ImageFileMachineAmd64;
        return true;
    }

    private static int ToInt32OrDefault(object? value, int fallback = 0)
    {
        if (value is int intValue)
        {
            return intValue;
        }

        if (value is string str
            && int.TryParse(str, out var parsed))
        {
            return parsed;
        }

        return fallback;
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

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool IsWow64Process(IntPtr processHandle, out bool wow64Process);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool IsWow64Process2(
        IntPtr processHandle,
        out ushort processMachine,
        out ushort nativeMachine);

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
