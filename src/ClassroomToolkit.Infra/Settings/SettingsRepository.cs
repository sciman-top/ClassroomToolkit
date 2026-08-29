using System.Security.Cryptography;
using ClassroomToolkit.Infra.Migration;

namespace ClassroomToolkit.Infra.Settings;

public sealed class SettingsRepository
{
    private readonly IniSettingsStore _store;
    private bool _hasValidatedExistingFileState;
    private bool _migrationBackupRequired;
    private long _lastValidatedWriteTimeUtcTicks = DateTime.MinValue.Ticks;
    private string? _lastValidatedContentHash;

    public bool LastLoadSucceeded { get; private set; } = true;

    public SettingsRepository(string path)
    {
        _store = new IniSettingsStore(path);
    }

    public Dictionary<string, Dictionary<string, string>> Load()
    {
        var loaded = _store.TryLoad(out var data);
        LastLoadSucceeded = loaded;
        _hasValidatedExistingFileState = true;
        _lastValidatedWriteTimeUtcTicks = GetCurrentWriteTimeUtcTicks();
        _lastValidatedContentHash = TryGetCurrentContentHash();
        _migrationBackupRequired = loaded
            && File.Exists(_store.Path)
            && SettingsMigrator.RequiresMigration(data);
        return SettingsMigrator.Migrate(data);
    }

    public void Save(Dictionary<string, Dictionary<string, string>> data)
    {
        ArgumentNullException.ThrowIfNull(data);

        EnsureExistingFileStateValidated();

        if (!LastLoadSucceeded && File.Exists(_store.Path))
        {
            throw new InvalidOperationException("设置文件读取失败，已阻止写入以避免覆盖原有配置。");
        }
        if (_migrationBackupRequired)
        {
            EnsureMigrationBackup();
        }
        // 深复制后注入元数据：外层与每个非空 section 都必须拷贝，否则调用方已包含
        // _meta 时版本号仍会写进调用方的内层字典，写盘失败后内存对象就与磁盘状态不一致。
        // null section 保持 null 原样传递，兼容“空 section”的既有语义。
        var dataToSave = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in data)
        {
            dataToSave[pair.Key] = pair.Value == null
                ? pair.Value!
                : new Dictionary<string, string>(pair.Value, StringComparer.OrdinalIgnoreCase);
        }
        if (!dataToSave.TryGetValue(SettingsMigrator.MetaSection, out var meta))
        {
            meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            dataToSave[SettingsMigrator.MetaSection] = meta;
        }
        meta[SettingsMigrator.VersionKey] = SettingsMigrator.CurrentVersion;
        _store.Save(dataToSave);
        LastLoadSucceeded = true;
        _migrationBackupRequired = false;
        _hasValidatedExistingFileState = true;
        _lastValidatedWriteTimeUtcTicks = GetCurrentWriteTimeUtcTicks();
        _lastValidatedContentHash = TryGetCurrentContentHash();
    }

    private void EnsureExistingFileStateValidated()
    {
        if (!File.Exists(_store.Path))
        {
            _hasValidatedExistingFileState = false;
            _migrationBackupRequired = false;
            _lastValidatedWriteTimeUtcTicks = DateTime.MinValue.Ticks;
            _lastValidatedContentHash = null;
            return;
        }

        var currentWriteTimeUtcTicks = GetCurrentWriteTimeUtcTicks();
        var currentContentHash = TryGetCurrentContentHash();
        if (_hasValidatedExistingFileState
            && _lastValidatedWriteTimeUtcTicks == currentWriteTimeUtcTicks
            && string.Equals(_lastValidatedContentHash, currentContentHash, StringComparison.Ordinal))
        {
            return;
        }

        LastLoadSucceeded = _store.TryLoad(out var existingData);
        _migrationBackupRequired = LastLoadSucceeded
            && SettingsMigrator.RequiresMigration(existingData);
        _hasValidatedExistingFileState = true;
        _lastValidatedWriteTimeUtcTicks = currentWriteTimeUtcTicks;
        _lastValidatedContentHash = currentContentHash ?? TryGetCurrentContentHash();
    }

    private long GetCurrentWriteTimeUtcTicks()
    {
        return File.Exists(_store.Path)
            ? File.GetLastWriteTimeUtc(_store.Path).Ticks
            : DateTime.MinValue.Ticks;
    }

    private string? GetCurrentContentHash()
    {
        if (!File.Exists(_store.Path))
        {
            return null;
        }

        using var stream = File.OpenRead(_store.Path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private string? TryGetCurrentContentHash()
    {
        try
        {
            return GetCurrentContentHash();
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private void EnsureMigrationBackup()
    {
        var contentHash = GetCurrentContentHash()
            ?? throw new IOException("无法读取旧设置文件，已阻止迁移覆盖。");
        var directory = Path.GetDirectoryName(_store.Path) ?? string.Empty;
        var fileName = Path.GetFileNameWithoutExtension(_store.Path);
        var extension = Path.GetExtension(_store.Path);
        var backupName = $"{fileName}.bak-v{SettingsMigrator.CurrentVersion}-{contentHash}{extension}";
        var backupPath = Path.Combine(directory, backupName);

        if (File.Exists(backupPath))
        {
            using var backupStream = File.OpenRead(backupPath);
            var backupHash = Convert.ToHexString(SHA256.HashData(backupStream));
            if (string.Equals(contentHash, backupHash, StringComparison.Ordinal))
            {
                return;
            }

            throw new IOException($"迁移备份路径已存在但内容不匹配：{backupPath}");
        }

        File.Copy(_store.Path, backupPath, overwrite: false);
    }
}
