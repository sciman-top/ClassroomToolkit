using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ClassroomToolkit.Domain.Models;
using ClassroomToolkit.Domain.Serialization;
using ClassroomToolkit.Domain.Services;
using ClassroomToolkit.Domain.Timers;
using ClassroomToolkit.Infra.Storage;

namespace ClassroomToolkit.App.ViewModels;

public sealed class RollCallViewModel : INotifyPropertyChanged
{
    private readonly string _dataPath;
    private StudentWorkbook? _workbook;
    private RollCallEngine? _engine;
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

    public RollCallViewModel(string dataPath)
    {
        _dataPath = dataPath;
        Groups = new ObservableCollection<string>();
        _timerEngine.SetCountdown(_timerMinutes, _timerSeconds);
        _timerEngine.TimerCompleted += () => TimerCompleted?.Invoke();
        _timerEngine.ReminderTriggered += () => ReminderTriggered?.Invoke();
        UpdateTimeDisplay();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? TimerCompleted;
    public event Action? ReminderTriggered;

    public ObservableCollection<string> Groups { get; }

    public string CurrentGroup
    {
        get => _currentGroup;
        private set => SetField(ref _currentGroup, value);
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

    public bool IsRollCallMode
    {
        get => _isRollCallMode;
        private set => SetField(ref _isRollCallMode, value);
    }

    public string ModeToggleLabel => IsRollCallMode ? "切换到计时" : "切换到点名";

    public string TimerModeLabel => _timerEngine.Mode == TimerMode.Countdown ? "倒计时" : "秒表";

    public string StartPauseLabel => _timerEngine.Running ? "暂停" : "开始";

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
            }
        }
    }

    public int InfoColumnCount
    {
        get
        {
            var count = (ShowId ? 1 : 0) + (ShowName ? 1 : 0);
            return Math.Max(1, count);
        }
    }

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
        private set => SetField(ref _activeClassName, value ?? string.Empty);
    }

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
            var minutes = Math.Max(0, value);
            if (SetField(ref _timerReminderIntervalMinutes, minutes))
            {
                ApplyReminderInterval();
            }
        }
    }

    public int TimerMinutes => _timerMinutes;

    public int TimerSeconds => _timerSeconds;

    public void LoadData()
    {
        var store = new StudentWorkbookStore();
        var result = store.LoadOrCreate(_dataPath);
        _workbook = result.Workbook;
        _engine = new RollCallEngine(_workbook.GetActiveRoster());
        ActiveClassName = _workbook.ActiveClass;

        if (!string.IsNullOrWhiteSpace(result.RollStateJson))
        {
            var states = RollStateSerializer.DeserializeWorkbookStates(result.RollStateJson);
            if (states.TryGetValue(_workbook.ActiveClass, out var state))
            {
                _engine.RestoreState(state);
            }
        }

        RefreshGroups();
        CurrentGroup = _engine.CurrentGroup;
        UpdateCurrentStudent();
    }

    public void RollNext()
    {
        var student = _engine?.RollNext();
        if (student == null)
        {
            CurrentStudentId = string.Empty;
            CurrentStudentName = string.Empty;
            return;
        }
        CurrentStudentId = student.StudentId;
        CurrentStudentName = student.Name;
    }

    public void ToggleMode()
    {
        IsRollCallMode = !IsRollCallMode;
        RaisePropertyChanged(nameof(ModeToggleLabel));
    }

    public void ToggleTimerMode()
    {
        var nextMode = _timerEngine.Mode == TimerMode.Countdown ? TimerMode.Stopwatch : TimerMode.Countdown;
        _timerEngine.SetMode(nextMode);
        UpdateTimeDisplay();
        RaisePropertyChanged(nameof(TimerModeLabel));
        RaisePropertyChanged(nameof(StartPauseLabel));
    }

    public void ToggleTimer()
    {
        _timerEngine.Toggle();
        RaisePropertyChanged(nameof(StartPauseLabel));
    }

    public void ResetTimer()
    {
        _timerEngine.Reset();
        UpdateTimeDisplay();
        RaisePropertyChanged(nameof(StartPauseLabel));
    }

    public void SetCountdown(int minutes, int seconds)
    {
        _timerMinutes = Math.Max(0, minutes);
        _timerSeconds = Math.Clamp(seconds, 0, 59);
        _timerEngine.SetCountdown(_timerMinutes, _timerSeconds);
        UpdateTimeDisplay();
    }

    public void TickTimer(TimeSpan elapsed)
    {
        _timerEngine.Tick(elapsed);
        UpdateTimeDisplay();
        RaisePropertyChanged(nameof(StartPauseLabel));
    }

    public void ResetCurrentGroup()
    {
        if (_engine == null)
        {
            return;
        }
        _engine.ResetGroup(CurrentGroup);
        CurrentStudentId = string.Empty;
        CurrentStudentName = string.Empty;
    }

    public void SetCurrentGroup(string group)
    {
        if (_engine == null)
        {
            return;
        }
        _engine.SetCurrentGroup(group);
        CurrentGroup = _engine.CurrentGroup;
        CurrentStudentId = string.Empty;
        CurrentStudentName = string.Empty;
    }

    public void SaveState()
    {
        if (_engine == null || _workbook == null)
        {
            return;
        }
        var state = _engine.CaptureState();
        var states = new Dictionary<string, ClassRollState>(StringComparer.OrdinalIgnoreCase)
        {
            [_workbook.ActiveClass] = state
        };
        var json = RollStateSerializer.SerializeWorkbookStates(states);
        var store = new StudentWorkbookStore();
        store.Save(_workbook, _dataPath, json);
    }

    public void SetRemotePresenterKey(string value)
    {
        RemotePresenterKey = string.IsNullOrWhiteSpace(value) ? "tab" : value.Trim().ToLowerInvariant();
    }

    private void RefreshGroups()
    {
        Groups.Clear();
        if (_engine == null)
        {
            return;
        }
        foreach (var group in _engine.Roster.Groups)
        {
            Groups.Add(group);
        }
    }

    private void UpdateCurrentStudent()
    {
        if (_engine == null || !_engine.CurrentStudentIndex.HasValue)
        {
            CurrentStudentId = string.Empty;
            CurrentStudentName = string.Empty;
            return;
        }
        var student = _engine.Roster.Students[_engine.CurrentStudentIndex.Value];
        CurrentStudentId = student.StudentId;
        CurrentStudentName = student.Name;
    }

    private void UpdateTimeDisplay()
    {
        if (_timerEngine.Mode == TimerMode.Countdown)
        {
            TimeDisplay = FormatTime(_timerEngine.SecondsLeft);
        }
        else
        {
            TimeDisplay = FormatTime(_timerEngine.StopwatchSeconds);
        }
        RaisePropertyChanged(nameof(TimerModeLabel));
    }

    private static string FormatTime(int totalSeconds)
    {
        totalSeconds = Math.Max(0, totalSeconds);
        var span = TimeSpan.FromSeconds(totalSeconds);
        if (span.TotalHours >= 1)
        {
            return $"{(int)span.TotalHours:00}:{span.Minutes:00}:{span.Seconds:00}";
        }
        return $"{span.Minutes:00}:{span.Seconds:00}";
    }

    private void ApplyReminderInterval()
    {
        if (!TimerReminderEnabled || TimerReminderIntervalMinutes <= 0)
        {
            _timerEngine.ReminderIntervalSeconds = 0;
            return;
        }
        _timerEngine.ReminderIntervalSeconds = TimerReminderIntervalMinutes * 60;
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }

    private void RaisePropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
