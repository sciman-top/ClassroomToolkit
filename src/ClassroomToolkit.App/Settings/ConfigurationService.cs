using System.IO;
using System.Text.Json;

namespace ClassroomToolkit.App.Settings;

public sealed class ConfigurationService : IConfigurationService
{
    private const string AppSettingsFileName = "appsettings.json";
    private const string DefaultSettingsIniName = "settings.ini";
    private const string SolutionFileName = "ClassroomToolkit.sln";

    public ConfigurationService()
        : this(null)
    {
    }

    public ConfigurationService(string? baseDirectory)
    {
        BaseDirectory = ResolveAppRootDirectory(baseDirectory);
        SettingsIniPath = ResolveSettingsIniPath();
    }

    public string BaseDirectory { get; }

    public string SettingsIniPath { get; }

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

        resolvedPath = Path.IsPathRooted(configured)
            ? configured
            : Path.GetFullPath(Path.Combine(BaseDirectory, configured));
        return true;
    }

    private string GetDefaultSettingsIniPath()
    {
        return Path.Combine(BaseDirectory, DefaultSettingsIniName);
    }

    private static string ResolveAppRootDirectory(string? baseDirectory)
    {
        var start = string.IsNullOrWhiteSpace(baseDirectory)
            ? AppDomain.CurrentDomain.BaseDirectory
            : baseDirectory;

        var normalizedStart = Path.GetFullPath(start);
        var solutionDirectory = FindSolutionDirectory(normalizedStart);
        if (!string.IsNullOrWhiteSpace(solutionDirectory))
        {
            return solutionDirectory;
        }

        // Publish/packaged mode: use executable directory as stable root.
        return normalizedStart;
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
