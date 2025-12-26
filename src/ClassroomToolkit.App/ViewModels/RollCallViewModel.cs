using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ClassroomToolkit.Domain.Models;
using ClassroomToolkit.Domain.Serialization;
using ClassroomToolkit.Domain.Services;
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

    public RollCallViewModel(string dataPath)
    {
        _dataPath = dataPath;
        Groups = new ObservableCollection<string>();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

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

    public void LoadData()
    {
        var store = new StudentWorkbookStore();
        var result = store.LoadOrCreate(_dataPath);
        _workbook = result.Workbook;
        _engine = new RollCallEngine(_workbook.GetActiveRoster());

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

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
