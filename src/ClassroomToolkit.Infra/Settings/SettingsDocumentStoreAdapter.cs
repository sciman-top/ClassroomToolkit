using ClassroomToolkit.Application.Abstractions;
using ClassroomToolkit.Infra.Settings;

namespace ClassroomToolkit.Infra.Settings;

public sealed class SettingsDocumentStoreAdapter : ISettingsDocumentStore
{
    private readonly SettingsRepository _repository;

    public SettingsDocumentStoreAdapter(string settingsPath)
    {
        ArgumentNullException.ThrowIfNull(settingsPath);
        _repository = new SettingsRepository(settingsPath);
    }

    public Dictionary<string, Dictionary<string, string>> Load()
    {
        return _repository.Load();
    }

    public void Save(Dictionary<string, Dictionary<string, string>> data)
    {
        _repository.Save(data);
    }
}
