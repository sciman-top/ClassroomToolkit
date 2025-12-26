using ClassroomToolkit.Domain.Models;
using ClassroomToolkit.Domain.Utilities;

namespace ClassroomToolkit.Domain.Services;

public sealed class RollCallEngine
{
    private readonly Random _random = new();
    private ClassRoster _roster;
    private Dictionary<string, List<int>> _groupRemaining = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, HashSet<int>> _groupDrawnHistory = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, int?> _groupLastStudent = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<int> _globalDrawn = new();

    public RollCallEngine(ClassRoster roster)
    {
        _roster = roster;
        CurrentGroup = IdentityUtils.AllGroupName;
        RebuildPools();
    }

    public string CurrentGroup { get; private set; }

    public int? CurrentStudentIndex { get; private set; }

    public int? PendingStudentIndex { get; private set; }

    public ClassRoster Roster => _roster;

    public IReadOnlyDictionary<string, List<int>> GroupRemaining => _groupRemaining;

    public void SetRoster(ClassRoster roster)
    {
        _roster = roster;
        CurrentStudentIndex = null;
        PendingStudentIndex = null;
        CurrentGroup = IdentityUtils.AllGroupName;
        RebuildPools();
    }

    public void SetCurrentGroup(string? groupName)
    {
        if (string.IsNullOrWhiteSpace(groupName))
        {
            return;
        }
        var normalized = IdentityUtils.NormalizeGroupName(groupName);
        if (!_groupRemaining.ContainsKey(normalized))
        {
            normalized = IdentityUtils.AllGroupName;
        }
        CurrentGroup = normalized;
        EnsureGroupPool(normalized, forceReset: false);
    }

    public StudentRecord? RollNext()
    {
        if (_roster.Students.Count == 0)
        {
            return null;
        }
        var group = CurrentGroup;
        if (!_groupRemaining.TryGetValue(group, out var remaining) || remaining.Count == 0)
        {
            EnsureGroupPool(group, forceReset: true);
            if (!_groupRemaining.TryGetValue(group, out remaining) || remaining.Count == 0)
            {
                return null;
            }
        }
        var idx = _random.Next(remaining.Count);
        var studentIndex = remaining[idx];
        remaining.RemoveAt(idx);
        MarkStudentDrawn(studentIndex);
        CurrentStudentIndex = studentIndex;
        PendingStudentIndex = null;
        return _roster.Students[studentIndex];
    }

    public void ResetAll()
    {
        _groupDrawnHistory.Clear();
        _globalDrawn.Clear();
        _groupLastStudent.Clear();
        RebuildPools();
        CurrentStudentIndex = null;
        PendingStudentIndex = null;
        CurrentGroup = IdentityUtils.AllGroupName;
    }

    public void ResetGroup(string groupName)
    {
        var normalized = IdentityUtils.NormalizeGroupName(groupName);
        if (normalized == IdentityUtils.AllGroupName)
        {
            ResetAll();
            return;
        }
        if (!_groupRemaining.ContainsKey(normalized))
        {
            return;
        }
        if (_groupDrawnHistory.TryGetValue(normalized, out var drawn))
        {
            foreach (var index in drawn)
            {
                RemoveFromGlobalHistory(index, normalized);
            }
            _groupDrawnHistory.Remove(normalized);
        }
        _groupLastStudent.Remove(normalized);
        EnsureGroupPool(normalized, forceReset: true);
    }

    public ClassRollState CaptureState()
    {
        var state = new ClassRollState
        {
            CurrentGroup = CurrentGroup,
            CurrentStudent = CurrentStudentIndex.HasValue ? ToSaveKey(CurrentStudentIndex.Value) : null,
            PendingStudent = PendingStudentIndex.HasValue ? ToSaveKey(PendingStudentIndex.Value) : null
        };

        foreach (var pair in _groupRemaining)
        {
            var list = new List<string>();
            foreach (var index in pair.Value)
            {
                list.Add(ToSaveKey(index));
            }
            state.GroupRemaining[pair.Key] = list;
        }

        foreach (var pair in _groupLastStudent)
        {
            state.GroupLast[pair.Key] = pair.Value.HasValue ? ToSaveKey(pair.Value.Value) : null;
        }

        state.GlobalDrawn = _globalDrawn.Select(ToSaveKey).ToList();
        return state;
    }

    public void RestoreState(ClassRollState? state)
    {
        if (state == null)
        {
            return;
        }
        var lookup = BuildIndexLookup();
        _groupRemaining = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        _groupDrawnHistory = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
        _groupLastStudent = new Dictionary<string, int?>(StringComparer.OrdinalIgnoreCase);
        _globalDrawn = new HashSet<int>();

        foreach (var pair in state.GroupRemaining)
        {
            var resolved = ResolveIndexList(pair.Value, lookup);
            _groupRemaining[pair.Key] = resolved;
        }

        foreach (var pair in state.GroupLast)
        {
            var resolved = ResolveIndex(pair.Value, lookup);
            _groupLastStudent[pair.Key] = resolved;
        }

        foreach (var key in state.GlobalDrawn)
        {
            var idx = ResolveIndex(key, lookup);
            if (idx.HasValue)
            {
                _globalDrawn.Add(idx.Value);
            }
        }

        CurrentGroup = ResolveGroupName(state.CurrentGroup);
        CurrentStudentIndex = ResolveIndex(state.CurrentStudent, lookup);
        PendingStudentIndex = ResolveIndex(state.PendingStudent, lookup);

        // 修正缺失的分组池，避免恢复后无法点名。
        foreach (var group in _roster.Groups)
        {
            EnsureGroupPool(group, forceReset: false);
        }
    }

    private void RebuildPools()
    {
        _groupRemaining = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        _groupDrawnHistory = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
        _groupLastStudent = new Dictionary<string, int?>(StringComparer.OrdinalIgnoreCase);
        _globalDrawn = new HashSet<int>();

        foreach (var pair in _roster.GroupIndexMap)
        {
            _groupRemaining[pair.Key] = new List<int>(pair.Value);
        }
    }

    private void EnsureGroupPool(string groupName, bool forceReset)
    {
        if (!_roster.GroupIndexMap.TryGetValue(groupName, out var baseList))
        {
            return;
        }
        if (!_groupRemaining.TryGetValue(groupName, out var remaining))
        {
            remaining = new List<int>();
            _groupRemaining[groupName] = remaining;
        }
        if (!forceReset && remaining.Count > 0)
        {
            return;
        }
        remaining.Clear();
        foreach (var index in baseList)
        {
            if (_globalDrawn.Contains(index))
            {
                continue;
            }
            remaining.Add(index);
        }
        Shuffle(remaining);
    }

    private void MarkStudentDrawn(int studentIndex)
    {
        _globalDrawn.Add(studentIndex);
        if (!_groupDrawnHistory.TryGetValue(CurrentGroup, out var history))
        {
            history = new HashSet<int>();
            _groupDrawnHistory[CurrentGroup] = history;
        }
        history.Add(studentIndex);
        _groupLastStudent[CurrentGroup] = studentIndex;

        foreach (var pair in _groupRemaining)
        {
            pair.Value.Remove(studentIndex);
        }
    }

    private void RemoveFromGlobalHistory(int studentIndex, string? ignoreGroup)
    {
        foreach (var pair in _groupDrawnHistory)
        {
            if (!string.IsNullOrWhiteSpace(ignoreGroup) && pair.Key.Equals(ignoreGroup, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            if (pair.Value.Contains(studentIndex))
            {
                return;
            }
        }
        _globalDrawn.Remove(studentIndex);
    }

    private void Shuffle(List<int> values)
    {
        for (var i = values.Count - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }
    }

    private string ToSaveKey(int index)
    {
        if (index < 0 || index >= _roster.Students.Count)
        {
            return string.Empty;
        }
        var student = _roster.Students[index];
        if (!string.IsNullOrWhiteSpace(student.RowKey))
        {
            return student.RowKey;
        }
        return student.RowId;
    }

    private static string ResolveGroupName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return IdentityUtils.AllGroupName;
        }
        return IdentityUtils.NormalizeGroupName(value);
    }

    private (Dictionary<string, int> RowIdMap, Dictionary<string, int> RowKeyMap) BuildIndexLookup()
    {
        var rowIdMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var rowKeyMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var duplicateKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < _roster.Students.Count; i++)
        {
            var student = _roster.Students[i];
            if (!string.IsNullOrWhiteSpace(student.RowId))
            {
                rowIdMap[student.RowId] = i;
            }
            if (!string.IsNullOrWhiteSpace(student.RowKey))
            {
                if (rowKeyMap.ContainsKey(student.RowKey))
                {
                    duplicateKeys.Add(student.RowKey);
                }
                else
                {
                    rowKeyMap[student.RowKey] = i;
                }
            }
        }
        foreach (var key in duplicateKeys)
        {
            rowKeyMap.Remove(key);
        }
        return (rowIdMap, rowKeyMap);
    }

    private int? ResolveIndex(string? key, (Dictionary<string, int> RowIdMap, Dictionary<string, int> RowKeyMap) lookup)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }
        if (key.StartsWith("rk:", StringComparison.OrdinalIgnoreCase))
        {
            if (lookup.RowKeyMap.TryGetValue(key, out var idx))
            {
                return idx;
            }
        }
        if (lookup.RowIdMap.TryGetValue(key, out var rowIdx))
        {
            return rowIdx;
        }
        if (lookup.RowKeyMap.TryGetValue(key, out var keyIdx))
        {
            return keyIdx;
        }
        if (int.TryParse(key, out var rawIdx))
        {
            if (rawIdx >= 0 && rawIdx < _roster.Students.Count)
            {
                return rawIdx;
            }
        }
        return null;
    }

    private List<int> ResolveIndexList(IEnumerable<string> keys, (Dictionary<string, int> RowIdMap, Dictionary<string, int> RowKeyMap) lookup)
    {
        var list = new List<int>();
        foreach (var key in keys)
        {
            var idx = ResolveIndex(key, lookup);
            if (idx.HasValue && !list.Contains(idx.Value))
            {
                list.Add(idx.Value);
            }
        }
        return list;
    }
}
