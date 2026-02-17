using ClassroomToolkit.App.Settings;

namespace ClassroomToolkit.App;

public sealed class RollCallWindowFactory : IRollCallWindowFactory
{
    private readonly AppSettingsService _settingsService;
    private readonly AppSettings _settings;
    private readonly ClassroomToolkit.Services.Input.GlobalHookService _hookService;
    private readonly ClassroomToolkit.Services.Speech.SpeechService _speechService;

    public RollCallWindowFactory(
        AppSettingsService settingsService, 
        AppSettings settings,
        ClassroomToolkit.Services.Input.GlobalHookService hookService,
        ClassroomToolkit.Services.Speech.SpeechService speechService)
    {
        _settingsService = settingsService;
        _settings = settings;
        _hookService = hookService;
        _speechService = speechService;
    }

    public RollCallWindow Create(string dataPath)
    {
        return new RollCallWindow(dataPath, _settingsService, _settings, _hookService, _speechService);
    }
}
