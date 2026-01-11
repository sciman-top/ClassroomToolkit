using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ClassroomToolkit.App.Models;
using ClassroomToolkit.App.Photos;
using ClassroomToolkit.Domain.Models;
using ClassroomToolkit.Domain.Serialization;
using ClassroomToolkit.Domain.Services;
using ClassroomToolkit.Domain.Timers;
using ClassroomToolkit.Domain.Utilities;
using ClassroomToolkit.Infra.Storage;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace ClassroomToolkit.App.ViewModels;

public sealed class RollCallViewModel : INotifyPropertyChanged
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

    public event PropertyChangedEventHandler? PropertyChanged;
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

    private void UpdateGroupSelection()
    {
        if (GroupButtons == null) return;
        foreach (var btn in GroupButtons)
        {
            if (btn.IsReset) continue;
            btn.IsSelected = string.Equals(btn.Label, _currentGroup, StringComparison.OrdinalIgnoreCase);
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
                RaisePropertyChanged(nameof(ModeToggleLabel));
                RaisePropertyChanged(nameof(ModeTitle));
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

    public int TimerMinutes => _timerMinutes;

    public int TimerSeconds => _timerSeconds;

    public void LoadData(string? preferredClass = null)
    {
        var result = LoadDataCore();
        ApplyLoadResult(result, preferredClass);
    }

    public async Task LoadDataAsync(string? preferredClass, Dispatcher dispatcher)
    {
        var result = await Task.Run(LoadDataCore).ConfigureAwait(false);
        await dispatcher.InvokeAsync(() =>
        {
            ApplyLoadResult(result, preferredClass);
        }, DispatcherPriority.Render);
    }

    private RollCallLoadResult LoadDataCore()
    {
        try
        {
            var store = new StudentWorkbookStore();
            var result = store.LoadOrCreate(_dataPath);
            var states = new Dictionary<string, ClassRollState>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in RollStateSerializer.DeserializeWorkbookStates(result.RollStateJson))
            {
                states[pair.Key] = pair.Value;
            }
            return new RollCallLoadResult(result.Workbook, states, null);
        }
        catch (Exception ex)
        {
            var fallbackRoster = new ClassRoster("班级1", Array.Empty<StudentRecord>());
            var fallbackWorkbook = new StudentWorkbook(
                new Dictionary<string, ClassRoster> { ["班级1"] = fallbackRoster },
                "班级1");
            var message = $"学生名册读取失败：{ex.Message}";
            return new RollCallLoadResult(fallbackWorkbook, new Dictionary<string, ClassRollState>(), message);
        }
    }

    private void ApplyLoadResult(RollCallLoadResult result, string? preferredClass)
    {
        _workbook = result.Workbook;
        _classStates.Clear();
        foreach (var pair in result.ClassStates)
        {
            _classStates[pair.Key] = pair.Value;
        }
        if (!string.IsNullOrWhiteSpace(preferredClass) &&
            _workbook.ClassNames.Any(name => name.Equals(preferredClass.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            _workbook.SetActiveClass(preferredClass.Trim());
        }
        _engine = new RollCallEngine(_workbook.GetActiveRoster());
        ActiveClassName = _workbook.ActiveClass;
        AvailableClasses = _workbook.ClassNames;
        RaisePropertyChanged(nameof(HasStudentData));

        if (_classStates.TryGetValue(_workbook.ActiveClass, out var state))
        {
            _engine.RestoreState(state);
        }

        RefreshGroups();
        CurrentGroup = _engine.CurrentGroup;
        UpdateCurrentStudent();
        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            DataLoadFailed?.Invoke(result.ErrorMessage);
        }
    }

    private sealed record RollCallLoadResult(
        StudentWorkbook Workbook,
        Dictionary<string, ClassRollState> ClassStates,
        string? ErrorMessage);

    public bool SwitchClass(string className, bool updatePhoto = true)
    {
        if (_engine == null || _workbook == null)
        {
            return false;
        }
        if (string.IsNullOrWhiteSpace(className))
        {
            return false;
        }
        var trimmed = className.Trim();
        if (!_workbook.ClassNames.Any(name => name.Equals(trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }
        if (trimmed.Equals(_workbook.ActiveClass, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        StoreCurrentState();
        _workbook.SetActiveClass(trimmed);
        _engine.SetRoster(_workbook.GetActiveRoster());
        ActiveClassName = _workbook.ActiveClass;
        RaisePropertyChanged(nameof(HasStudentData));
        if (_classStates.TryGetValue(_workbook.ActiveClass, out var state))
        {
            _engine.RestoreState(state);
        }
        RefreshGroups();
        CurrentGroup = _engine.CurrentGroup;
        UpdateCurrentStudent(updatePhoto);
        return true;
    }

    public bool TryBuildStudentList(out IReadOnlyList<StudentListItem> students, out string? message)
    {
        students = Array.Empty<StudentListItem>();
        message = null;
        if (_engine == null)
        {
            message = "暂无学生数据，无法显示名单。";
            return false;
        }
        var roster = _engine.Roster;
        if (roster.Students.Count == 0)
        {
            message = "暂无学生数据，无法显示名单。";
            return false;
        }

        var groupKey = ResolveGroupKey(CurrentGroup);
        if (!_engine.GroupAll.TryGetValue(groupKey, out var indices))
        {
            _engine.GroupAll.TryGetValue(IdentityUtils.AllGroupName, out indices);
        }
        indices ??= new List<int>();
        if (indices.Count == 0)
        {
            message = CurrentGroup == IdentityUtils.AllGroupName
                ? "当前没有可显示的学生名单。"
                : "当前分组没有可显示的学生名单。";
            return false;
        }

        var remaining = _engine.GroupRemaining.TryGetValue(groupKey, out var pool)
            ? new HashSet<int>(pool)
            : new HashSet<int>();
        var list = new List<(StudentSortKey Key, StudentListItem Item)>();
        foreach (var idx in indices)
        {
            if (idx < 0 || idx >= roster.Students.Count)
            {
                continue;
            }
            var student = roster.Students[idx];
            var displayId = IdentityUtils.CompactText(student.StudentId);
            var displayName = IdentityUtils.NormalizeText(student.Name);
            var key = BuildStudentSortKey(displayId, displayName);
            var item = new StudentListItem(displayId, displayName, idx, !remaining.Contains(idx));
            list.Add((key, item));
        }

        if (list.Count == 0)
        {
            message = "当前没有可显示的学生名单。";
            return false;
        }

        list.Sort((a, b) => a.Key.CompareTo(b.Key));
        students = list.Select(item => item.Item).ToList();
        return true;
    }

    private string ResolveGroupKey(string group)
    {
        if (_engine == null)
        {
            return group;
        }
        var normalized = IdentityUtils.NormalizeGroupName(group);
        if (_engine.GroupAll.ContainsKey(normalized))
        {
            return normalized;
        }
        var trimmed = normalized.Trim();
        if (trimmed.EndsWith("组", StringComparison.Ordinal))
        {
            var stripped = trimmed[..^1];
            if (_engine.GroupAll.ContainsKey(stripped))
            {
                return stripped;
            }
        }
        else
        {
            var withSuffix = $"{trimmed}组";
            if (_engine.GroupAll.ContainsKey(withSuffix))
            {
                return withSuffix;
            }
        }
        foreach (var key in _engine.GroupAll.Keys)
        {
            if (NormalizeGroupKey(key) == NormalizeGroupKey(trimmed))
            {
                return key;
            }
        }
        return normalized;
    }

    private static string NormalizeGroupKey(string group)
    {
        var normalized = IdentityUtils.NormalizeGroupName(group);
        if (normalized.EndsWith("组", StringComparison.Ordinal))
        {
            normalized = normalized[..^1];
        }
        return normalized;
    }

    public bool SetCurrentStudentByIndex(int index)
    {
        if (_engine == null)
        {
            return false;
        }
        if (index < 0 || index >= _engine.Roster.Students.Count)
        {
            return false;
        }
        _engine.SetCurrentStudentIndex(index);
        UpdateCurrentStudent();
        return true;
    }

    public bool TryRollNext(out string? message)
    {
        message = null;
        if (_engine == null)
        {
            message = "暂无学生数据，无法点名。";
            SetPlaceholderStudent();
            return false;
        }
        if (_engine.Roster.Students.Count == 0)
        {
            message = "当前没有可点名的学生。";
            SetPlaceholderStudent();
            return false;
        }
        var groupKey = ResolveGroupKey(CurrentGroup);
        if (!_engine.GroupRemaining.TryGetValue(groupKey, out var remaining) || remaining.Count == 0)
        {
            if (!_engine.GroupAll.TryGetValue(groupKey, out var baseList) || baseList.Count == 0)
            {
                message = $"'{CurrentGroup}' 分组当前没有可点名的学生。";
                SetPlaceholderStudent();
                return false;
            }
            if (_engine.AllGroupsCompleted())
            {
                message = "所有学生都已完成点名，请点击“重置”按钮重新开始。";
            }
            else
            {
                message = $"'{CurrentGroup}' 的同学已经全部点到，请切换其他分组或点击“重置”按钮。";
            }
            return false;
        }
        var student = _engine.RollNext();
        if (student == null)
        {
            SetPlaceholderStudent();
            return false;
        }
        CurrentStudentId = IdentityUtils.CompactText(student.StudentId);
        CurrentStudentName = IdentityUtils.NormalizeText(student.Name);
        return true;
    }

    public void ToggleMode()
    {
        IsRollCallMode = !IsRollCallMode;
    }

    public void ToggleTimerMode()
    {
        if (_timerEngine.Running)
        {
            return;
        }
        var nextMode = _timerEngine.Mode switch
        {
            TimerMode.Countdown => TimerMode.Stopwatch,
            TimerMode.Stopwatch => TimerMode.Clock,
            _ => TimerMode.Countdown
        };
        _timerEngine.SetMode(nextMode);
        ApplyReminderInterval();
        UpdateTimeDisplay();
        RaisePropertyChanged(nameof(TimerModeLabel));
        RaisePropertyChanged(nameof(StartPauseLabel));
        RaisePropertyChanged(nameof(CurrentTimerMode));
    }

    public void ToggleTimer()
    {
        if (_timerEngine.Mode == TimerMode.Clock)
        {
            return;
        }
        if (!_timerEngine.Running && _timerEngine.Mode == TimerMode.Countdown && _timerEngine.SecondsLeft <= 0)
        {
            _timerEngine.Reset();
            UpdateTimeDisplay();
        }
        _timerEngine.Toggle();
        RaisePropertyChanged(nameof(StartPauseLabel));
    }

    public void ResetTimer()
    {
        if (_timerEngine.Mode == TimerMode.Clock)
        {
            return;
        }
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

    public void ApplyTimerState(bool isRollCallMode, TimerMode timerMode, int minutes, int seconds, int secondsLeft, int stopwatchSeconds, bool running)
    {
        IsRollCallMode = isRollCallMode;
        _timerMinutes = Math.Max(0, minutes);
        _timerSeconds = Math.Clamp(seconds, 0, 59);
        var countdownTotal = Math.Max(0, _timerMinutes * 60 + _timerSeconds);
        if (secondsLeft <= 0)
        {
            secondsLeft = countdownTotal;
        }
        _timerEngine.SetState(timerMode, countdownTotal, secondsLeft, stopwatchSeconds, running);
        ApplyReminderInterval();
        UpdateTimeDisplay();
        RaisePropertyChanged(nameof(TimerModeLabel));
        RaisePropertyChanged(nameof(StartPauseLabel));
        RaisePropertyChanged(nameof(CurrentTimerMode));
    }

    public void TickTimer(TimeSpan elapsed)
    {
        if (_timerEngine.Mode == TimerMode.Clock)
        {
            UpdateTimeDisplay();
            return;
        }
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
        _engine.SetCurrentStudentIndex(null);
        SetPlaceholderStudent();
    }

    public void SetCurrentGroup(string group)
    {
        if (_engine == null)
        {
            return;
        }
        _engine.SetCurrentGroup(group);
        CurrentGroup = _engine.CurrentGroup;
        SetPlaceholderStudent();
    }

    public void SaveState()
    {
        if (_engine == null || _workbook == null)
        {
            return;
        }
        try
        {
            StoreCurrentState();
            var json = RollStateSerializer.SerializeWorkbookStates(new Dictionary<string, ClassRollState>(_classStates, StringComparer.OrdinalIgnoreCase));
            var store = new StudentWorkbookStore();
            store.Save(_workbook, _dataPath, json);
        }
        catch (Exception ex)
        {
            var message = $"学生名册保存失败：{ex.Message}";
            DataSaveFailed?.Invoke(message);
        }
    }

    public void SetRemotePresenterKey(string value)
    {
        RemotePresenterKey = string.IsNullOrWhiteSpace(value) ? "tab" : value.Trim().ToLowerInvariant();
    }

    public void ResetCurrentStudentDisplay()
    {
        SetPlaceholderStudent();
    }

    private void RefreshGroups()
    {
        Groups.Clear();
        GroupButtons.Clear();
        if (_engine == null)
        {
            return;
        }
        foreach (var group in _engine.Roster.Groups)
        {
            Groups.Add(group);
            GroupButtons.Add(new GroupButtonItem(group, isReset: false));
        }
        GroupButtons.Add(new GroupButtonItem("重置", isReset: true));
        UpdateGroupSelection();
    }

    private void UpdateCurrentStudent(bool updatePhoto = true)
    {
        if (_engine == null || !_engine.CurrentStudentIndex.HasValue)
        {
            SetPlaceholderStudent();
            return;
        }
        var student = _engine.Roster.Students[_engine.CurrentStudentIndex.Value];
        CurrentStudentId = IdentityUtils.CompactText(student.StudentId);
        CurrentStudentName = IdentityUtils.NormalizeText(student.Name);
        
        if (updatePhoto)
        {
            var className = string.IsNullOrWhiteSpace(PhotoSharedClass) ? ActiveClassName : PhotoSharedClass;
            CurrentStudentPhotoPath = _photoResolver.ResolvePhotoPath(className, CurrentStudentId);
        }
        else
        {
            CurrentStudentPhotoPath = null;
        }
    }

    private void SetPlaceholderStudent()
    {
        CurrentStudentId = ShowId ? "学号" : string.Empty;
        CurrentStudentName = ShowName ? "学生" : string.Empty;
        CurrentStudentPhotoPath = null;
    }

    private void UpdateTimeDisplay()
    {
        if (_timerEngine.Mode == TimerMode.Clock)
        {
            TimeDisplay = DateTime.Now.ToString("HH:mm:ss");
        }
        else if (_timerEngine.Mode == TimerMode.Countdown)
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
        if (_timerEngine.Mode != TimerMode.Countdown || !TimerReminderEnabled || TimerReminderIntervalMinutes <= 0)
        {
            _timerEngine.ReminderIntervalSeconds = 0;
            return;
        }
        _timerEngine.ReminderIntervalSeconds = TimerReminderIntervalMinutes * 60;
    }

    private void StoreCurrentState()
    {
        if (_engine == null || _workbook == null)
        {
            return;
        }
        _classStates[_workbook.ActiveClass] = _engine.CaptureState();
    }

    private bool AreAllGroupsCompleted()
    {
        if (_engine == null)
        {
            return false;
        }
        return _engine.AllGroupsCompleted();
    }

    private static StudentSortKey BuildStudentSortKey(string studentId, string studentName)
    {
        var id = IdentityUtils.CompactText(studentId);
        var nameKey = (studentName ?? string.Empty).Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(id) && int.TryParse(id, out var numeric))
        {
            return new StudentSortKey(0, numeric, string.Empty, nameKey, id);
        }
        if (!string.IsNullOrWhiteSpace(id))
        {
            return new StudentSortKey(1, 0, id.ToLowerInvariant(), nameKey, id);
        }
        return new StudentSortKey(2, 0, string.Empty, nameKey, string.Empty);
    }

    private readonly record struct StudentSortKey(int Category, int NumericId, string TextId, string NameKey, string RawId)
        : IComparable<StudentSortKey>
    {
        public int CompareTo(StudentSortKey other)
        {
            var cmp = Category.CompareTo(other.Category);
            if (cmp != 0)
            {
                return cmp;
            }
            if (Category == 0)
            {
                cmp = NumericId.CompareTo(other.NumericId);
                if (cmp != 0)
                {
                    return cmp;
                }
            }
            else if (Category == 1)
            {
                cmp = string.Compare(TextId, other.TextId, StringComparison.OrdinalIgnoreCase);
                if (cmp != 0)
                {
                    return cmp;
                }
            }
            cmp = string.Compare(NameKey, other.NameKey, StringComparison.OrdinalIgnoreCase);
            if (cmp != 0)
            {
                return cmp;
            }
            return string.Compare(RawId, other.RawId, StringComparison.OrdinalIgnoreCase);
        }
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
