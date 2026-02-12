using ClassroomToolkit.Infra.Migration;

namespace ClassroomToolkit.Infra.Settings;

public sealed class SettingsRepository
{
    private readonly IniSettingsStore _store;
    public bool LastLoadSucceeded { get; private set; } = true;

    public SettingsRepository(string path)
    {
        _store = new IniSettingsStore(path);
    }

    public Dictionary<string, Dictionary<string, string>> Load()
    {
        var loaded = _store.TryLoad(out var data);
        LastLoadSucceeded = loaded;
        return SettingsMigrator.Migrate(data, _store.Path);
    }

    public void Save(Dictionary<string, Dictionary<string, string>> data)
    {
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
    }
}
