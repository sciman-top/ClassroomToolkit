using ClassroomToolkit.Infra.Migration;

namespace ClassroomToolkit.Infra.Settings;

public sealed class SettingsRepository
{
    private readonly IniSettingsStore _store;

    public SettingsRepository(string path)
    {
        _store = new IniSettingsStore(path);
    }

    public Dictionary<string, Dictionary<string, string>> Load()
    {
        var data = _store.Load();
        return SettingsMigrator.Migrate(data, _store.Path);
    }

    public void Save(Dictionary<string, Dictionary<string, string>> data)
    {
        if (!data.TryGetValue(SettingsMigrator.MetaSection, out var meta))
        {
            meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            data[SettingsMigrator.MetaSection] = meta;
        }
        meta[SettingsMigrator.VersionKey] = SettingsMigrator.CurrentVersion;
        _store.Save(data);
    }
}
