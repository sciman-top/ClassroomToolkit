using System;
using System.IO;
using System.Text.Json;
using ClassroomToolkit.App.Startup;

namespace ClassroomToolkit.App.Settings;

internal sealed class ConfigurationService : IConfigurationService
{
    private const string AppSettingsFileName = "appsettings.json";
    private const string DefaultSettingsIniName = "settings.ini";
    private const string DefaultSettingsJsonName = "settings.json";
    private const string SolutionFileName = "ClassroomToolkit.sln";

    private readonly string _defaultSettingsRoot;
    private readonly JsonElement _appSettingsRoot;
    private readonly bool _hasAppSettings;

    public ConfigurationService()
        : this(null)
    {
    }

    public ConfigurationService(string? baseDirectory)
    {
        BaseDirectory = ResolveAppRootDirectory(baseDirectory);
        _defaultSettingsRoot = ResolveDefaultSettingsRoot(baseDirectory, BaseDirectory);
        // appsettings.json 只解析一次：SettingsIniPath 与 SettingsDocument 此前各自
        // File.Exists+OpenRead+Parse 同一文件（每实例双解析）。
        (_hasAppSettings, _appSettingsRoot) = TryParseAppSettingsRoot(BaseDirectory);
        SettingsIniPath = ResolveSettingsIniPath();
        (SettingsDocumentFormat, SettingsDocumentPath) = ResolveSettingsDocument();
    }

    private static (bool HasRoot, JsonElement Root) TryParseAppSettingsRoot(string baseDirectory)
    {
        var appSettingsPath = Path.Combine(baseDirectory, AppSettingsFileName);
        if (!File.Exists(appSettingsPath))
        {
            return (false, default);
        }

        try
        {
            using var stream = File.OpenRead(appSettingsPath);
            using var document = JsonDocument.Parse(stream);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return (false, default);
            }

            // Clone 脱离 document 生命周期；实例常驻进程，无需显式释放。
            return (true, document.RootElement.Clone());
        }
        catch (JsonException)
        {
            return (false, default);
        }
        catch (IOException)
        {
            return (false, default);
        }
        catch (UnauthorizedAccessException)
        {
            return (false, default);
        }
    }

    public string BaseDirectory { get; }

    public string SettingsIniPath { get; }

    public SettingsDocumentFormat SettingsDocumentFormat { get; }

    public string SettingsDocumentPath { get; }

    private string ResolveSettingsIniPath()
    {
        if (!_hasAppSettings)
        {
            return GetDefaultSettingsIniPath();
        }

        var root = _appSettingsRoot;
        if (TryReadSettingPath(root, "SettingsIniPath", out var direct))
        {
            return direct;
        }

        if (root.TryGetProperty("Paths", out var pathsNode)
            && TryReadSettingPath(pathsNode, "SettingsIni", out var nested))
        {
            return nested;
        }

        return GetDefaultSettingsIniPath();
    }

    private bool TryReadSettingPath(JsonElement element, string key, out string resolvedPath)
    {
        resolvedPath = string.Empty;
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(key, out var node)
            || node.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var configured = node.GetString();
        if (string.IsNullOrWhiteSpace(configured))
        {
            return false;
        }

        try
        {
            resolvedPath = Path.IsPathRooted(configured)
                ? configured
                : Path.GetFullPath(Path.Combine(BaseDirectory, configured));
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch (PathTooLongException)
        {
            return false;
        }
    }

    private string GetDefaultSettingsIniPath()
    {
        return Path.Combine(_defaultSettingsRoot, DefaultSettingsIniName);
    }

    private string GetDefaultSettingsJsonPath()
    {
        return Path.Combine(_defaultSettingsRoot, DefaultSettingsJsonName);
    }

    private (SettingsDocumentFormat format, string path) ResolveSettingsDocument()
    {
        if (!_hasAppSettings)
        {
            return (SettingsDocumentFormat.Json, GetDefaultSettingsJsonPath());
        }

        var root = _appSettingsRoot;
        var hasPathsNode = root.TryGetProperty("Paths", out var pathsNode);

        var configuredFormat = TryReadDocumentFormat(root, "SettingsDocumentFormat")
            ?? (hasPathsNode
                ? TryReadDocumentFormat(pathsNode, "SettingsDocumentFormat")
                : null);

        var configuredPath = TryReadSettingPath(root, "SettingsDocumentPath", out var directPath)
            ? directPath
            : hasPathsNode
                && (TryReadSettingPath(pathsNode, "SettingsDocument", out var nestedPath)
                    || TryReadSettingPath(pathsNode, "SettingsJson", out nestedPath))
                    ? nestedPath
                    : string.Empty;

        if (configuredFormat == null && !string.IsNullOrWhiteSpace(configuredPath))
        {
            configuredFormat = Path.GetExtension(configuredPath).Equals(".json", StringComparison.OrdinalIgnoreCase)
                ? SettingsDocumentFormat.Json
                : SettingsDocumentFormat.Ini;
        }

        // Compatibility default: when appsettings.json exists but does not configure
        // settings document keys, keep legacy INI document mode.
        var format = configuredFormat ?? SettingsDocumentFormat.Ini;
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return format switch
            {
                SettingsDocumentFormat.Json => (format, GetDefaultSettingsJsonPath()),
                _ => (SettingsDocumentFormat.Ini, SettingsIniPath)
            };
        }

        return (format, configuredPath);
    }

    private static SettingsDocumentFormat? TryReadDocumentFormat(JsonElement element, string key)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(key, out var node)
            || node.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var raw = node.GetString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return raw.Trim().Equals("json", StringComparison.OrdinalIgnoreCase)
            ? SettingsDocumentFormat.Json
            : raw.Trim().Equals("ini", StringComparison.OrdinalIgnoreCase)
                ? SettingsDocumentFormat.Ini
                : null;
    }

    private static string ResolveAppRootDirectory(string? baseDirectory)
    {
        var start = string.IsNullOrWhiteSpace(baseDirectory)
            ? AppDomain.CurrentDomain.BaseDirectory
            : baseDirectory;

        string normalizedStart;
        try
        {
            normalizedStart = Path.GetFullPath(start);
        }
        catch (ArgumentException)
        {
            normalizedStart = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory);
        }
        catch (NotSupportedException)
        {
            normalizedStart = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory);
        }
        catch (PathTooLongException)
        {
            normalizedStart = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory);
        }

        var solutionDirectory = FindSolutionDirectory(normalizedStart);
        if (!string.IsNullOrWhiteSpace(solutionDirectory))
        {
            return solutionDirectory;
        }

        // Publish/packaged mode: use executable directory as stable root.
        return normalizedStart;
    }

    private static string ResolveDefaultSettingsRoot(string? requestedBaseDirectory, string appRootDirectory)
        => ResolveDefaultSettingsRoot(requestedBaseDirectory, appRootDirectory, AppDomain.CurrentDomain.BaseDirectory);

    internal static string ResolveDefaultSettingsRoot(
        string? requestedBaseDirectory,
        string appRootDirectory,
        string runtimeBaseDirectory)
    {
        if (PortableRuntimeContext.TryResolveRoot(runtimeBaseDirectory, out var portableRoot))
        {
            return Path.Combine(portableRoot, PortableRuntimeContext.DataFolderName);
        }

        if (HasValidExplicitBaseDirectory(requestedBaseDirectory))
        {
            return appRootDirectory;
        }

        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(local))
        {
            return Path.Combine(local, "ClassroomToolkit");
        }

        return appRootDirectory;
    }

    private static bool HasValidExplicitBaseDirectory(string? requestedBaseDirectory)
    {
        if (string.IsNullOrWhiteSpace(requestedBaseDirectory))
        {
            return false;
        }

        try
        {
            _ = Path.GetFullPath(requestedBaseDirectory);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch (PathTooLongException)
        {
            return false;
        }
    }

    private static string? FindSolutionDirectory(string start)
    {
        if (string.IsNullOrWhiteSpace(start))
        {
            return null;
        }

        DirectoryInfo? current;
        try
        {
            current = new DirectoryInfo(Path.GetFullPath(start));
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
        catch (PathTooLongException)
        {
            return null;
        }

        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, SolutionFileName)))
            {
                return current.FullName;
            }
            current = current.Parent;
        }

        return null;
    }
}
