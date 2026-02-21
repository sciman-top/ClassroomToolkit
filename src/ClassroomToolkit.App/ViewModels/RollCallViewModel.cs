using System.Collections.ObjectModel;
using ClassroomToolkit.App.Models;
using ClassroomToolkit.App.Photos;
using ClassroomToolkit.Domain.Models;
using ClassroomToolkit.Domain.Serialization;
using ClassroomToolkit.Domain.Services;
using ClassroomToolkit.Domain.Timers;
using ClassroomToolkit.Domain.Utilities;
using ClassroomToolkit.Infra.Storage;

namespace ClassroomToolkit.App.ViewModels;

public sealed partial class RollCallViewModel : ViewModelBase
{
    private readonly string _dataPath;
    private StudentWorkbook? _workbook;
    private RollCallEngine? _engine;
    private readonly Dictionary<string, ClassRollState> _classStates = new(StringComparer.OrdinalIgnoreCase);
    private string _currentGroup = "全部";
    private string _currentStudentId = string.Empty;
    private string _currentStudentName = string.Empty;
    private bool _isRollCallMode = true;
    private string _timeDisplay = "00:00";
    private readonly TimerEngine _timerEngine = new();
    private int _timerMinutes = 5;
    private int _timerSeconds;
    private bool _showId = true;
    private bool _showName = true;
    private bool _remotePresenterEnabled;
    private bool _showPhoto;
    private int _photoDurationSeconds;
    private string _photoSharedClass = string.Empty;
    private string _remotePresenterKey = "tab";
    private string _activeClassName = string.Empty;
    private bool _timerSoundEnabled = true;
    private bool _timerReminderEnabled;
    private int _timerReminderIntervalMinutes;
    private bool _speechEnabled;
    private string _timerSoundVariant = "gentle";
    private string _timerReminderSoundVariant = "soft_beep";
    private string _speechEngine = "pyttsx3";
    private string _speechVoiceId = string.Empty;
    private string _speechOutputId = string.Empty;
    private IReadOnlyList<string> _availableClasses = Array.Empty<string>();
    private bool _canPersistWorkbook = true;

    private readonly StudentPhotoResolver _photoResolver;
    private string? _currentStudentPhotoPath;

    public RollCallViewModel(string dataPath)
    {
        _dataPath = dataPath;
        _photoResolver = new StudentPhotoResolver("student_photos");
        Groups = new ObservableCollection<string>();
        GroupButtons = new ObservableCollection<GroupButtonItem>();
        _timerEngine.SetCountdown(_timerMinutes, _timerSeconds);
        _timerEngine.TimerCompleted += () => TimerCompleted?.Invoke();
        _timerEngine.ReminderTriggered += () => ReminderTriggered?.Invoke();
        UpdateTimeDisplay();
    }

    public event Action? TimerCompleted;
    public event Action? ReminderTriggered;
    public event Action<string>? DataLoadFailed;
    public event Action<string>? DataSaveFailed;

    public ObservableCollection<string> Groups { get; }
    public ObservableCollection<GroupButtonItem> GroupButtons { get; }

    public string CurrentGroup
    {
        get => _currentGroup;
        private set
        {
            if (SetField(ref _currentGroup, value))
            {
                UpdateGroupSelection();
            }
        }
    }

    public string CurrentStudentId
    {
        get => _currentStudentId;
        private set => SetField(ref _currentStudentId, value);
    }

    public string CurrentStudentName
    {
        get => _currentStudentName;
        private set => SetField(ref _currentStudentName, value);
    }

    public string? CurrentStudentPhotoPath
    {
        get => _currentStudentPhotoPath;
        private set => SetField(ref _currentStudentPhotoPath, value);
    }

    public bool IsRollCallMode
    {
        get => _isRollCallMode;
        private set
        {
            if (SetField(ref _isRollCallMode, value))
            {
                RaisePropertyChanged(nameof(ModeToggleLabel), nameof(ModeTitle));
            }
        }
    }

    public string ModeToggleLabel => IsRollCallMode ? "切换到计时" : "切换到点名";
    public string ModeTitle => IsRollCallMode ? "点名" : "计时";

    public string TimerModeLabel => _timerEngine.Mode switch
    {
        TimerMode.Countdown => "倒计时",
        TimerMode.Stopwatch => "秒表",
        _ => "时钟"
    };

    public string StartPauseLabel => _timerEngine.Mode == TimerMode.Clock ? "开始" : (_timerEngine.Running ? "暂停" : "开始");
    public TimerMode CurrentTimerMode => _timerEngine.Mode;
    public bool TimerRunning => _timerEngine.Running;
    public int TimerSecondsLeft => _timerEngine.SecondsLeft;
    public int TimerStopwatchSeconds => _timerEngine.StopwatchSeconds;
    public int TimerCountdownSeconds => _timerEngine.CountdownSeconds;

    public string TimeDisplay
    {
        get => _timeDisplay;
        private set => SetField(ref _timeDisplay, value);
    }

    public string RemotePresenterKey
    {
        get => _remotePresenterKey;
        set => SetField(ref _remotePresenterKey, value);
    }

    public bool ShowId
    {
        get => _showId;
        set
        {
            if (SetField(ref _showId, value))
            {
                RaisePropertyChanged(nameof(InfoColumnCount));
                UpdateCurrentStudent();
            }
        }
    }

    public bool ShowName
    {
        get => _showName;
        set
        {
            if (SetField(ref _showName, value))
            {
                RaisePropertyChanged(nameof(InfoColumnCount));
                UpdateCurrentStudent();
            }
        }
    }

    public int InfoColumnCount => Math.Max(1, (ShowId ? 1 : 0) + (ShowName ? 1 : 0));

    public bool RemotePresenterEnabled
    {
        get => _remotePresenterEnabled;
        set => SetField(ref _remotePresenterEnabled, value);
    }

    public bool ShowPhoto
    {
        get => _showPhoto;
        set => SetField(ref _showPhoto, value);
    }

    public int PhotoDurationSeconds
    {
        get => _photoDurationSeconds;
        set => SetField(ref _photoDurationSeconds, Math.Max(0, value));
    }

    public string PhotoSharedClass
    {
        get => _photoSharedClass;
        set => SetField(ref _photoSharedClass, value ?? string.Empty);
    }

    public string ActiveClassName
    {
        get => _activeClassName;
        private set
        {
            if (SetField(ref _activeClassName, value ?? string.Empty))
            {
                RaisePropertyChanged(nameof(ClassButtonLabel));
            }
        }
    }

    public string ClassButtonLabel => string.IsNullOrWhiteSpace(ActiveClassName) ? "班级" : ActiveClassName;
    public bool HasStudentData => _engine?.Roster.Students.Count > 0;

    public IReadOnlyList<string> AvailableClasses
    {
        get => _availableClasses;
        private set => SetField(ref _availableClasses, value ?? Array.Empty<string>());
    }

    public bool CanPersistWorkbook => _canPersistWorkbook;

    public bool TimerSoundEnabled
    {
        get => _timerSoundEnabled;
        set => SetField(ref _timerSoundEnabled, value);
    }

    public bool TimerReminderEnabled
    {
        get => _timerReminderEnabled;
        set
        {
            if (SetField(ref _timerReminderEnabled, value))
            {
                ApplyReminderInterval();
            }
        }
    }

    public int TimerReminderIntervalMinutes
    {
        get => _timerReminderIntervalMinutes;
        set
        {
            if (SetField(ref _timerReminderIntervalMinutes, Math.Max(0, value)))
            {
                ApplyReminderInterval();
            }
        }
    }

    public bool SpeechEnabled
    {
        get => _speechEnabled;
        set => SetField(ref _speechEnabled, value);
    }

    public string TimerSoundVariant
    {
        get => _timerSoundVariant;
        set => SetField(ref _timerSoundVariant, value ?? "gentle");
    }

    public string TimerReminderSoundVariant
    {
        get => _timerReminderSoundVariant;
        set => SetField(ref _timerReminderSoundVariant, value ?? "soft_beep");
    }

    public string SpeechEngine
    {
        get => _speechEngine;
        set => SetField(ref _speechEngine, value ?? "pyttsx3");
    }

    public string SpeechVoiceId
    {
        get => _speechVoiceId;
        set => SetField(ref _speechVoiceId, value ?? string.Empty);
    }

    public string SpeechOutputId
    {
        get => _speechOutputId;
        set => SetField(ref _speechOutputId, value ?? string.Empty);
    }

    private string _remoteGroupSwitchKey = "b";
    private bool _remoteGroupSwitchEnabled = false;

    public string RemoteGroupSwitchKey
    {
        get => _remoteGroupSwitchKey;
        set => SetField(ref _remoteGroupSwitchKey, value ?? "b");
    }

    public bool RemoteGroupSwitchEnabled
    {
        get => _remoteGroupSwitchEnabled;
        set => SetField(ref _remoteGroupSwitchEnabled, value);
    }

    public int TimerMinutes => _timerMinutes;
    public int TimerSeconds => _timerSeconds;

    public void ToggleMode() => IsRollCallMode = !IsRollCallMode;
    public void SetRemotePresenterKey(string key) => RemotePresenterKey = key;
    public void ResetCurrentStudentDisplay() => UpdateCurrentStudent();
}
