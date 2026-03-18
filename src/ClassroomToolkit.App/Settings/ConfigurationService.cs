using System;
using System.IO;
using System.Text.Json;

namespace ClassroomToolkit.App.Settings;

public sealed class ConfigurationService : IConfigurationService
{
    private const string AppSettingsFileName = "appsettings.json";
    private const string DefaultSettingsIniName = "settings.ini";
    private const string DefaultSettingsJsonName = "settings.json";
    private const string SolutionFileName = "ClassroomToolkit.sln";

    private readonly string _defaultSettingsRoot;

    public ConfigurationService()
        : this(null)
    {
    }

    public ConfigurationService(string? baseDirectory)
    {
        BaseDirectory = ResolveAppRootDirectory(baseDirectory);
        _defaultSettingsRoot = ResolveDefaultSettingsRoot(baseDirectory, BaseDirectory);
        SettingsIniPath = ResolveSettingsIniPath();
        (SettingsDocumentFormat, SettingsDocumentPath) = ResolveSettingsDocument();
    }

    public string BaseDirectory { get; }

    public string SettingsIniPath { get; }

    public SettingsDocumentFormat SettingsDocumentFormat { get; }

    public string SettingsDocumentPath { get; }

    private string ResolveSettingsIniPath()
    {
        var appSettingsPath = Path.Combine(BaseDirectory, AppSettingsFileName);
        if (!File.Exists(appSettingsPath))
        {
            return GetDefaultSettingsIniPath();
        }

        try
        {
            using var stream = File.OpenRead(appSettingsPath);
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;

            if (TryReadSettingPath(root, "SettingsIniPath", out var direct))
            {
                return direct;
            }

            if (root.TryGetProperty("Paths", out var pathsNode)
                && TryReadSettingPath(pathsNode, "SettingsIni", out var nested))
            {
                return nested;
            }
        }
        catch (JsonException)
        {
            // Fall back to default settings.ini when appsettings.json is malformed.
        }
        catch (IOException)
        {
            // Fall back to default settings.ini when appsettings.json cannot be read.
        }
        catch (UnauthorizedAccessException)
        {
            // Fall back to default settings.ini when appsettings.json cannot be accessed.
        }

        return GetDefaultSettingsIniPath();
    }

    private bool TryReadSettingPath(JsonElement element, string key, out string resolvedPath)
    {
        resolvedPath = string.Empty;
        if (!element.TryGetProperty(key, out var node) || node.ValueKind != JsonValueKind.String)
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
        var appSettingsPath = Path.Combine(BaseDirectory, AppSettingsFileName);
        if (!File.Exists(appSettingsPath))
        {
            return (SettingsDocumentFormat.Json, GetDefaultSettingsJsonPath());
        }

        try
        {
            using var stream = File.OpenRead(appSettingsPath);
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;
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
        catch (JsonException)
        {
            // Fall back to default INI when appsettings.json is malformed.
        }
        catch (IOException)
        {
            // Fall back to default INI when appsettings.json cannot be read.
        }
        catch (UnauthorizedAccessException)
        {
            // Fall back to default INI when appsettings.json cannot be accessed.
        }

        return (SettingsDocumentFormat.Json, GetDefaultSettingsJsonPath());
    }

    private static SettingsDocumentFormat? TryReadDocumentFormat(JsonElement element, string key)
    {
        if (!element.TryGetProperty(key, out var node) || node.ValueKind != JsonValueKind.String)
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
    {
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
