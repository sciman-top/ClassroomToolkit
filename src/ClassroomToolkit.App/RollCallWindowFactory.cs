using ClassroomToolkit.App.Settings;

namespace ClassroomToolkit.App;

public sealed class RollCallWindowFactory : IRollCallWindowFactory
{
    private readonly AppSettingsService _settingsService;
    private readonly AppSettings _settings;

    public RollCallWindowFactory(AppSettingsService settingsService, AppSettings settings)
    {
        _settingsService = settingsService;
        _settings = settings;
    }

    public RollCallWindow Create(string dataPath)
    {
        return new RollCallWindow(dataPath, _settingsService, _settings);
    }
}
