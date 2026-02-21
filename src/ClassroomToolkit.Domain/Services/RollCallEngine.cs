using ClassroomToolkit.Domain.Models;
using ClassroomToolkit.Domain.Utilities;

namespace ClassroomToolkit.Domain.Services;

public sealed partial class RollCallEngine
{
    private readonly Random _random = new();
    private ClassRoster _roster;
    private Dictionary<string, List<int>> _groupAll = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, List<int>> _groupRemaining = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, List<int>> _groupInitialSequences = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, HashSet<int>> _groupDrawnHistory = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, int?> _groupLastStudent = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<int> _globalDrawn = new();
    private Dictionary<int, HashSet<string>> _studentGroups = new();
    private HashSet<string> _duplicateRowKeys = new(StringComparer.OrdinalIgnoreCase);

    public RollCallEngine(ClassRoster roster)
    {
        _roster = roster;
        CurrentGroup = IdentityUtils.AllGroupName;
        RebuildGroupIndices();
    }

    public string CurrentGroup { get; private set; }

    public int? CurrentStudentIndex { get; private set; }

    public int? PendingStudentIndex { get; private set; }

    public ClassRoster Roster => _roster;

    public IReadOnlyDictionary<string, List<int>> GroupRemaining => _groupRemaining;

    public IReadOnlyDictionary<string, List<int>> GroupAll => _groupAll;

    public void SetRoster(ClassRoster roster)
    {
        _roster = roster;
        CurrentStudentIndex = null;
        PendingStudentIndex = null;
        CurrentGroup = IdentityUtils.AllGroupName;
        RebuildGroupIndices();
    }

    public void SetCurrentGroup(string? groupName)
    {
        if (string.IsNullOrWhiteSpace(groupName))
        {
            return;
        }
        var normalized = IdentityUtils.NormalizeGroupName(groupName);
        if (!_groupAll.ContainsKey(normalized))
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
        ValidateAndRepairState("roll_next");
        var group = CurrentGroup;
        if (!_groupRemaining.TryGetValue(group, out var remaining) || remaining.Count == 0)
        {
            EnsureGroupPool(group, forceReset: false);
            if (!_groupRemaining.TryGetValue(group, out remaining) || remaining.Count == 0)
            {
                return null;
            }
        }
        var index = remaining.Count > 1 ? _random.Next(remaining.Count) : 0;
        var studentIndex = remaining[index];
        remaining.RemoveAt(index);
        CurrentStudentIndex = studentIndex;
        PendingStudentIndex = studentIndex;
        _groupLastStudent[group] = studentIndex;
        MarkStudentDrawn(studentIndex);
        return _roster.Students[studentIndex];
    }

    public void ResetAll()
    {
        _groupDrawnHistory.Clear();
        _globalDrawn.Clear();
        _groupLastStudent.Clear();
        PendingStudentIndex = null;
        CurrentStudentIndex = null;
        CurrentGroup = IdentityUtils.AllGroupName;
        RebuildGroupIndices();
        EnsureGroupPool(CurrentGroup, forceReset: false);
        ValidateAndRepairState("reset_all");
    }

    public void ResetGroup(string groupName)
    {
        var normalized = IdentityUtils.NormalizeGroupName(groupName);
        if (normalized == IdentityUtils.AllGroupName)
        {
            ResetAll();
            return;
        }
        if (!_groupAll.TryGetValue(normalized, out var baseList) || baseList.Count == 0)
        {
            return;
        }
        var history = _groupDrawnHistory.GetValueOrDefault(normalized);
        if (history != null)
        {
            foreach (var index in history.ToList())
            {
                RemoveFromGlobalHistory(index, normalized);
            }
            history.Clear();
        }
        var baseIndices = CollectBaseIndices(baseList);
        var shuffled = baseIndices.Where(idx => !_globalDrawn.Contains(idx)).ToList();
        Shuffle(shuffled);
        _groupRemaining[normalized] = shuffled;
        _groupInitialSequences[normalized] = new List<int>(shuffled);
        _groupLastStudent[normalized] = null;
        if (PendingStudentIndex.HasValue && baseIndices.Contains(PendingStudentIndex.Value))
        {
            PendingStudentIndex = null;
        }
        RefreshAllGroupPool();
        ValidateAndRepairState($"reset_group:{normalized}");
    }

    public bool AllGroupsCompleted()
    {
        var totalStudents = _groupAll.TryGetValue(IdentityUtils.AllGroupName, out var total)
            ? total.Count
            : 0;
        if (totalStudents == 0)
        {
            return true;
        }
        if (_globalDrawn.Count < totalStudents)
        {
            return false;
        }
        foreach (var group in _groupAll.Keys)
        {
            if (_groupRemaining.TryGetValue(group, out var remaining) && remaining.Count > 0)
            {
                return false;
            }
        }
        return true;
    }

    public void SetCurrentStudentIndex(int? index)
    {
        if (!index.HasValue)
        {
            CurrentStudentIndex = null;
            PendingStudentIndex = null;
            return;
        }
        var value = index.Value;
        if (value < 0 || value >= _roster.Students.Count)
        {
            return;
        }
        CurrentStudentIndex = value;
        PendingStudentIndex = null;
    }

    private void Shuffle(List<int> values)
    {
        for (var i = values.Count - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }
    }

    private static List<int> NormalizeIndices(IEnumerable<int> values, HashSet<int>? allowed = null)
    {
        var normalized = new List<int>();
        var seen = new HashSet<int>();
        foreach (var value in values)
        {
            if (allowed != null && !allowed.Contains(value))
            {
                continue;
            }
            if (!seen.Add(value))
            {
                continue;
            }
            normalized.Add(value);
        }
        return normalized;
    }

    private static List<int> CollectBaseIndices(IEnumerable<int>? values)
    {
        return values == null ? new List<int>() : NormalizeIndices(values);
    }

    private static bool SequenceEquals(IReadOnlyList<int> left, IReadOnlyList<int> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }
        for (var i = 0; i < left.Count; i++)
        {
            if (left[i] != right[i])
            {
                return false;
            }
        }
        return true;
    }

    private static int? SanitizeIndex(int? value, HashSet<int> allowed)
    {
        if (!value.HasValue)
        {
            return null;
        }
        return allowed.Contains(value.Value) ? value : null;
    }

    private static string ResolveGroupName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return IdentityUtils.AllGroupName;
        }
        return IdentityUtils.NormalizeGroupName(value);
    }
}
