using System.IO;
using System.Text.Json;

namespace ClassroomToolkit.App.Settings;

public sealed class ConfigurationService : IConfigurationService
{
    private const string AppSettingsFileName = "appsettings.json";
    private const string DefaultSettingsIniName = "settings.ini";

    public ConfigurationService()
        : this(null)
    {
    }

    public ConfigurationService(string? baseDirectory)
    {
        BaseDirectory = string.IsNullOrWhiteSpace(baseDirectory)
            ? AppDomain.CurrentDomain.BaseDirectory
            : baseDirectory;
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
}
