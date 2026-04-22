using System.Globalization;
using System.Diagnostics.CodeAnalysis;

namespace ClassroomToolkit.Infra.Settings;

public sealed record SettingsDocumentMigrationResult(
    bool Migrated,
    int SectionCount,
    int KeyCount,
    string SourcePath,
    string TargetPath,
    string? BackupPath);

public sealed class SettingsDocumentMigrationService
{
    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Kept as instance API to avoid breaking existing composition-root construction code.")]
    public SettingsDocumentMigrationResult MigrateIniToJson(
        string iniPath,
        string jsonPath,
        bool overwriteJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(iniPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(jsonPath);

        var sourcePath = Path.GetFullPath(iniPath);
        var targetPath = Path.GetFullPath(jsonPath);
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("INI settings file not found.", sourcePath);
        }

        var iniStore = new IniSettingsStore(sourcePath);
        if (!iniStore.TryLoad(out var data))
        {
            throw new InvalidDataException($"INI settings file cannot be parsed: {sourcePath}");
        }

        string? backupPath = null;
        if (File.Exists(targetPath))
        {
            if (!overwriteJson)
            {
                return new SettingsDocumentMigrationResult(
                    Migrated: false,
                    SectionCount: 0,
                    KeyCount: 0,
                    SourcePath: sourcePath,
                    TargetPath: targetPath,
                    BackupPath: null);
            }

            backupPath = CreateBackupPath(targetPath);
            File.Copy(targetPath, backupPath, overwrite: false);
        }

        var jsonStore = new JsonSettingsDocumentStoreAdapter(targetPath);
        jsonStore.Save(data);

        var sectionCount = data.Count;
        var keyCount = data.Sum(section => section.Value.Count);
        return new SettingsDocumentMigrationResult(
            Migrated: true,
            SectionCount: sectionCount,
            KeyCount: keyCount,
            SourcePath: sourcePath,
            TargetPath: targetPath,
            BackupPath: backupPath);
    }

    private static string CreateBackupPath(string targetPath)
    {
        var directory = Path.GetDirectoryName(targetPath) ?? string.Empty;
        var fileName = Path.GetFileNameWithoutExtension(targetPath);
        var extension = Path.GetExtension(targetPath);
        var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        return Path.Combine(directory, $"{fileName}.bak-{timestamp}-{Guid.NewGuid():N}{extension}");
    }
}
