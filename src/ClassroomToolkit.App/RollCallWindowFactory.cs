using ClassroomToolkit.App.Settings;
using ClassroomToolkit.Application.UseCases.RollCall;

namespace ClassroomToolkit.App;

public sealed class RollCallWindowFactory : IRollCallWindowFactory
{
    private readonly AppSettingsService _settingsService;
    private readonly AppSettings _settings;
    private readonly ClassroomToolkit.Services.Input.GlobalHookService _hookService;
    private readonly ClassroomToolkit.Services.Speech.SpeechService _speechService;
    private readonly RollCallWorkbookUseCase _rollCallWorkbookUseCase;

    public RollCallWindowFactory(
        AppSettingsService settingsService, 
        AppSettings settings,
        ClassroomToolkit.Services.Input.GlobalHookService hookService,
        ClassroomToolkit.Services.Speech.SpeechService speechService,
        RollCallWorkbookUseCase rollCallWorkbookUseCase)
    {
        _settingsService = settingsService;
        _settings = settings;
        _hookService = hookService;
        _speechService = speechService;
        _rollCallWorkbookUseCase = rollCallWorkbookUseCase;
    }

    public RollCallWindow Create(string dataPath)
    {
        return new RollCallWindow(dataPath, _settingsService, _settings, _hookService, _speechService, _rollCallWorkbookUseCase);
    }
}
