using System.Globalization;

namespace ClassroomToolkit.Infra.Migration;

public static class SettingsMigrator
{
    public const string CurrentVersion = "2.0";
    public const string MetaSection = "_meta";
    public const string VersionKey = "_settings_version";

    public static Dictionary<string, Dictionary<string, string>> Migrate(
        Dictionary<string, Dictionary<string, string>> data,
        string? settingsPath)
    {
        if (!data.TryGetValue(MetaSection, out var meta))
        {
            meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            data[MetaSection] = meta;
        }
        var existingVersion = meta.TryGetValue(VersionKey, out var version) ? version : string.Empty;
        if (string.Equals(existingVersion, CurrentVersion, StringComparison.OrdinalIgnoreCase))
        {
            return data;
        }

        if (!string.IsNullOrWhiteSpace(settingsPath) && File.Exists(settingsPath))
        {
            CreateBackup(settingsPath);
        }

        // 未来版本迁移逻辑放在这里。
        meta[VersionKey] = CurrentVersion;
        return data;
    }

    private static void CreateBackup(string path)
    {
        var directory = Path.GetDirectoryName(path) ?? string.Empty;
        var fileName = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        var backupName = $"{fileName}.bak-{timestamp}{ext}";
        var backupPath = Path.Combine(directory, backupName);
        try
        {
            File.Copy(path, backupPath, overwrite: false);
        }
        catch
        {
            // 备份失败不阻塞主流程。
        }
    }
}
