using System.Security.Cryptography;
using ClassroomToolkit.Infra.Migration;

namespace ClassroomToolkit.Infra.Settings;

public sealed class SettingsRepository
{
    private readonly IniSettingsStore _store;
    private bool _hasValidatedExistingFileState;
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
        return SettingsMigrator.Migrate(data, _store.Path);
    }

    public void Save(Dictionary<string, Dictionary<string, string>> data)
    {
        ArgumentNullException.ThrowIfNull(data);

        EnsureExistingFileStateValidated();

        if (!LastLoadSucceeded && File.Exists(_store.Path))
        {
            throw new InvalidOperationException("设置文件读取失败，已阻止写入以避免覆盖原有配置。");
        }
        if (!data.TryGetValue(SettingsMigrator.MetaSection, out var meta))
        {
            meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            data[SettingsMigrator.MetaSection] = meta;
        }
        meta[SettingsMigrator.VersionKey] = SettingsMigrator.CurrentVersion;
        _store.Save(data);
        LastLoadSucceeded = true;
        _hasValidatedExistingFileState = true;
        _lastValidatedWriteTimeUtcTicks = GetCurrentWriteTimeUtcTicks();
        _lastValidatedContentHash = TryGetCurrentContentHash();
    }

    private void EnsureExistingFileStateValidated()
    {
        if (!File.Exists(_store.Path))
        {
            _hasValidatedExistingFileState = false;
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

        LastLoadSucceeded = _store.TryLoad(out _);
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
}
