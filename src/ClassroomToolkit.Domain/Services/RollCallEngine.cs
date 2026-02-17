using ClassroomToolkit.Domain.Models;
using ClassroomToolkit.Domain.Utilities;

namespace ClassroomToolkit.Domain.Services;

public sealed class RollCallEngine
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

    public void RestoreState(ClassRollState? state)
    {
        if (state == null)
        {
            return;
        }
        var stateGroupRemaining = state.GroupRemaining
            ?? new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var stateGroupLast = state.GroupLast
            ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var stateGlobalDrawn = state.GlobalDrawn ?? new List<string>();

        var lookup = BuildIndexLookup();
        _groupRemaining = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        _groupDrawnHistory = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
        _groupLastStudent = new Dictionary<string, int?>(StringComparer.OrdinalIgnoreCase);
        _globalDrawn = new HashSet<int>();

        foreach (var pair in stateGroupRemaining)
        {
            var resolved = ResolveIndexList(pair.Value, lookup);
            _groupRemaining[pair.Key] = resolved;
        }

        foreach (var pair in stateGroupLast)
        {
            var resolved = ResolveIndex(pair.Value, lookup);
            _groupLastStudent[pair.Key] = resolved;
        }

        foreach (var key in stateGlobalDrawn)
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

        foreach (var group in _roster.Groups)
        {
            EnsureGroupPool(group, forceReset: false);
        }
        ValidateAndRepairState("restore_state");
    }

    private void RebuildGroupIndices()
    {
        _duplicateRowKeys = BuildDuplicateRowKeys();
        _groupAll = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        _groupRemaining = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        _groupLastStudent = new Dictionary<string, int?>(StringComparer.OrdinalIgnoreCase);
        _groupInitialSequences = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        _groupDrawnHistory = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
        _globalDrawn = new HashSet<int>();
        _studentGroups = new Dictionary<int, HashSet<string>>();

        var totalIndices = Enumerable.Range(0, _roster.Students.Count).ToList();
        _groupAll[IdentityUtils.AllGroupName] = totalIndices;
        foreach (var idx in totalIndices)
        {
            _studentGroups[idx] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                IdentityUtils.AllGroupName
            };
        }

        foreach (var group in _roster.Groups)
        {
            if (group.Equals(IdentityUtils.AllGroupName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            if (_roster.GroupIndexMap.TryGetValue(group, out var indices))
            {
                _groupAll[group] = new List<int>(indices);
                foreach (var idx in indices)
                {
                    if (!_studentGroups.TryGetValue(idx, out var set))
                    {
                        set = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                        {
                            IdentityUtils.AllGroupName
                        };
                        _studentGroups[idx] = set;
                    }
                    set.Add(group);
                }
            }
        }

        foreach (var pair in _groupAll)
        {
            var pool = new List<int>(pair.Value);
            Shuffle(pool);
            _groupRemaining[pair.Key] = pool;
            _groupInitialSequences[pair.Key] = new List<int>(pool);
            _groupLastStudent[pair.Key] = null;
            _groupDrawnHistory[pair.Key] = new HashSet<int>();
        }
        _groupDrawnHistory[IdentityUtils.AllGroupName] = _globalDrawn;
        RefreshAllGroupPool();
    }

    private void ValidateAndRepairState(string context)
    {
        var baseAll = CollectBaseIndices(_groupAll.GetValueOrDefault(IdentityUtils.AllGroupName));
        var baseAllSet = new HashSet<int>(baseAll);
        var changed = false;

        var knownGroups = new HashSet<string>(_groupAll.Keys, StringComparer.OrdinalIgnoreCase);
        foreach (var group in _roster.Groups)
        {
            knownGroups.Add(group);
        }

        foreach (var group in _groupRemaining.Keys.ToList())
        {
            if (!knownGroups.Contains(group))
            {
                _groupRemaining.Remove(group);
                changed = true;
            }
        }
        foreach (var group in _groupLastStudent.Keys.ToList())
        {
            if (!knownGroups.Contains(group))
            {
                _groupLastStudent.Remove(group);
                changed = true;
            }
        }
        foreach (var group in _groupDrawnHistory.Keys.ToList())
        {
            if (!knownGroups.Contains(group) && !group.Equals(IdentityUtils.AllGroupName, StringComparison.OrdinalIgnoreCase))
            {
                _groupDrawnHistory.Remove(group);
                changed = true;
            }
        }

        if (!ReferenceEquals(_groupDrawnHistory.GetValueOrDefault(IdentityUtils.AllGroupName), _globalDrawn))
        {
            _groupDrawnHistory[IdentityUtils.AllGroupName] = _globalDrawn;
            changed = true;
        }

        if (_globalDrawn.Count > 0)
        {
            var filtered = new HashSet<int>(_globalDrawn.Where(idx => baseAllSet.Contains(idx)));
            if (!filtered.SetEquals(_globalDrawn))
            {
                _globalDrawn = filtered;
                _groupDrawnHistory[IdentityUtils.AllGroupName] = _globalDrawn;
                changed = true;
            }
        }

        foreach (var pair in _groupAll)
        {
            var group = pair.Key;
            if (group.Equals(IdentityUtils.AllGroupName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            var baseList = CollectBaseIndices(pair.Value);
            var baseSet = new HashSet<int>(baseList);

            var historyRaw = _groupDrawnHistory.GetValueOrDefault(group) ?? new HashSet<int>();
            var history = new HashSet<int>(historyRaw.Where(idx => baseSet.Contains(idx)));
            if (!history.SetEquals(historyRaw))
            {
                _groupDrawnHistory[group] = history;
                changed = true;
            }
            if (history.Count > 0 && !history.IsSubsetOf(_globalDrawn))
            {
                _globalDrawn.UnionWith(history);
                _groupDrawnHistory[IdentityUtils.AllGroupName] = _globalDrawn;
                changed = true;
            }

            var poolRaw = _groupRemaining.GetValueOrDefault(group) ?? new List<int>();
            var normalizedPool = NormalizeIndices(poolRaw, baseSet);
            if (_globalDrawn.Count > 0 || history.Count > 0)
            {
                normalizedPool = normalizedPool.Where(idx => !_globalDrawn.Contains(idx) && !history.Contains(idx)).ToList();
            }
            if (!SequenceEquals(normalizedPool, poolRaw))
            {
                _groupRemaining[group] = new List<int>(normalizedPool);
                changed = true;
            }

            var lastValue = _groupLastStudent.GetValueOrDefault(group);
            if (lastValue.HasValue && baseSet.Count > 0 && !baseSet.Contains(lastValue.Value))
            {
                _groupLastStudent[group] = null;
                changed = true;
            }

            if (!_groupInitialSequences.TryGetValue(group, out var seqRaw) || seqRaw.Count == 0)
            {
                var seq = new List<int>(normalizedPool);
                foreach (var idx in baseList)
                {
                    if (!seq.Contains(idx))
                    {
                        seq.Add(idx);
                    }
                }
                _groupInitialSequences[group] = seq;
                changed = true;
            }
        }

        if (_roster.Groups.Count > 0)
        {
            if (!_roster.Groups.Contains(CurrentGroup, StringComparer.OrdinalIgnoreCase))
            {
                var fallback = _roster.Groups.Contains(IdentityUtils.AllGroupName, StringComparer.OrdinalIgnoreCase)
                    ? IdentityUtils.AllGroupName
                    : _roster.Groups[0];
                CurrentGroup = fallback;
                changed = true;
            }
        }
        else if (!string.IsNullOrWhiteSpace(CurrentGroup))
        {
            CurrentGroup = string.Empty;
            changed = true;
        }

        CurrentStudentIndex = SanitizeIndex(CurrentStudentIndex, baseAllSet);
        PendingStudentIndex = SanitizeIndex(PendingStudentIndex, baseAllSet);

        if (changed)
        {
            RefreshAllGroupPool();
        }
    }

    private void EnsureGroupPool(string groupName, bool forceReset)
    {
        if (!_groupAll.TryGetValue(groupName, out var baseList))
        {
            baseList = new List<int>();
            if (groupName.Equals(IdentityUtils.AllGroupName, StringComparison.OrdinalIgnoreCase))
            {
                baseList = Enumerable.Range(0, _roster.Students.Count).ToList();
            }
            else if (_roster.GroupIndexMap.TryGetValue(groupName, out var rawList))
            {
                baseList = new List<int>(rawList);
            }
            _groupAll[groupName] = baseList;
            _groupRemaining[groupName] = new List<int>();
            _groupLastStudent[groupName] = null;
            _groupDrawnHistory[groupName] = groupName.Equals(IdentityUtils.AllGroupName, StringComparison.OrdinalIgnoreCase)
                ? _globalDrawn
                : new HashSet<int>();
            foreach (var idx in baseList)
            {
                if (!_studentGroups.TryGetValue(idx, out var groups))
                {
                    groups = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        IdentityUtils.AllGroupName
                    };
                    _studentGroups[idx] = groups;
                }
                groups.Add(groupName);
            }
            var shuffled = new List<int>(baseList);
            Shuffle(shuffled);
            _groupInitialSequences[groupName] = shuffled;
        }

        var baseIndices = CollectBaseIndices(baseList);
        var baseSet = new HashSet<int>(baseIndices);
        var referenceDrawn = groupName.Equals(IdentityUtils.AllGroupName, StringComparison.OrdinalIgnoreCase)
            ? _globalDrawn
            : _groupDrawnHistory.GetValueOrDefault(groupName) ?? new HashSet<int>();

        if (groupName.Equals(IdentityUtils.AllGroupName, StringComparison.OrdinalIgnoreCase))
        {
            if (!_groupInitialSequences.ContainsKey(groupName))
            {
                var shuffled = new List<int>(baseIndices);
                Shuffle(shuffled);
                _groupInitialSequences[groupName] = shuffled;
            }
            RefreshAllGroupPool();
            _groupLastStudent.TryAdd(groupName, null);
            return;
        }

        if (forceReset || !_groupRemaining.ContainsKey(groupName))
        {
            referenceDrawn.Clear();
            var pool = new List<int>(baseIndices.Where(idx => !_globalDrawn.Contains(idx)));
            Shuffle(pool);
            _groupRemaining[groupName] = pool;
            _groupLastStudent[groupName] = null;
            _groupInitialSequences[groupName] = new List<int>(pool);
            RefreshAllGroupPool();
            return;
        }

        var rawPool = _groupRemaining.GetValueOrDefault(groupName) ?? new List<int>();
        var normalizedPool = new List<int>();
        var seen = new HashSet<int>();
        foreach (var value in rawPool)
        {
            if (!baseSet.Contains(value) || seen.Contains(value) || referenceDrawn.Contains(value))
            {
                continue;
            }
            normalizedPool.Add(value);
            seen.Add(value);
        }
        var sourceOrder = _groupInitialSequences.GetValueOrDefault(groupName);
        if (sourceOrder == null || sourceOrder.Count == 0)
        {
            sourceOrder = new List<int>(baseIndices);
            _groupInitialSequences[groupName] = new List<int>(sourceOrder);
        }
        foreach (var idx in sourceOrder)
        {
            if (referenceDrawn.Contains(idx) || seen.Contains(idx) || !baseSet.Contains(idx))
            {
                continue;
            }
            normalizedPool.Add(idx);
            seen.Add(idx);
        }
        var additional = new List<int>();
        foreach (var idx in baseIndices)
        {
            if (referenceDrawn.Contains(idx) || seen.Contains(idx))
            {
                continue;
            }
            additional.Add(idx);
            seen.Add(idx);
        }
        if (additional.Count > 0)
        {
            Shuffle(additional);
            foreach (var value in additional)
            {
                var insertAt = normalizedPool.Count > 0 ? _random.Next(normalizedPool.Count + 1) : 0;
                normalizedPool.Insert(insertAt, value);
            }
        }
        _groupRemaining[groupName] = normalizedPool;
        _groupInitialSequences[groupName] = new List<int>(normalizedPool);
        _groupLastStudent.TryAdd(groupName, null);
        RefreshAllGroupPool();
    }

    private void RefreshAllGroupPool()
    {
        var baseAllList = CollectBaseIndices(_groupAll.GetValueOrDefault(IdentityUtils.AllGroupName));
        var baseAllSet = new HashSet<int>(baseAllList);
        var subgroupBase = new Dictionary<string, (List<int> BaseList, HashSet<int> BaseSet)>(StringComparer.OrdinalIgnoreCase);
        var subgroupRemaining = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        var subgroupRemainingUnion = new HashSet<int>();
        var drawnFromSubgroups = new HashSet<int>();

        foreach (var pair in _groupAll)
        {
            var group = pair.Key;
            if (group.Equals(IdentityUtils.AllGroupName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            var baseList = CollectBaseIndices(pair.Value);
            var baseSet = new HashSet<int>(baseList);
            subgroupBase[group] = (baseList, baseSet);
            var pool = _groupRemaining.GetValueOrDefault(group) ?? new List<int>();
            var sanitized = NormalizeIndices(pool, baseSet);
            if (!SequenceEquals(sanitized, pool))
            {
                _groupRemaining[group] = sanitized;
            }
            subgroupRemaining[group] = sanitized;
            subgroupRemainingUnion.UnionWith(sanitized);
            var poolSet = new HashSet<int>(sanitized);
            drawnFromSubgroups.UnionWith(baseSet.Where(idx => !poolSet.Contains(idx)));

            if (!_groupInitialSequences.TryGetValue(group, out var initial) || initial.Count == 0)
            {
                _groupInitialSequences[group] = new List<int>(baseList);
            }
            else
            {
                var cleanedInitial = NormalizeIndices(initial, baseSet);
                if (!SequenceEquals(cleanedInitial, initial))
                {
                    _groupInitialSequences[group] = cleanedInitial;
                }
                foreach (var idx in baseList)
                {
                    if (!_groupInitialSequences[group].Contains(idx))
                    {
                        _groupInitialSequences[group].Add(idx);
                    }
                }
            }
        }

        var validGlobal = new HashSet<int>(_globalDrawn.Where(idx => baseAllSet.Contains(idx) && !subgroupRemainingUnion.Contains(idx)));
        var newGlobal = new HashSet<int>(drawnFromSubgroups.Where(idx => baseAllSet.Contains(idx)));
        newGlobal.UnionWith(validGlobal);
        _globalDrawn = newGlobal;
        _groupDrawnHistory[IdentityUtils.AllGroupName] = _globalDrawn;

        foreach (var pair in subgroupBase)
        {
            var group = pair.Key;
            var baseSet = pair.Value.BaseSet;
            var pool = subgroupRemaining.GetValueOrDefault(group) ?? new List<int>();
            var filtered = pool.Where(idx => baseSet.Contains(idx) && !_globalDrawn.Contains(idx)).ToList();
            if (!SequenceEquals(filtered, pool))
            {
                _groupRemaining[group] = filtered;
                pool = filtered;
            }
            var drawnSet = new HashSet<int>(baseSet.Where(idx => !pool.Contains(idx)));
            _groupDrawnHistory[group] = drawnSet;
        }

        if (!_groupInitialSequences.TryGetValue(IdentityUtils.AllGroupName, out var orderHint) || orderHint.Count == 0)
        {
            var shuffled = new List<int>(baseAllList);
            Shuffle(shuffled);
            orderHint = shuffled;
        }
        else
        {
            var cleanedAll = NormalizeIndices(orderHint, baseAllSet);
            orderHint = cleanedAll;
        }
        var orderSet = new HashSet<int>(orderHint);
        foreach (var idx in baseAllList)
        {
            if (!orderSet.Contains(idx))
            {
                orderHint.Add(idx);
                orderSet.Add(idx);
            }
        }
        _groupInitialSequences[IdentityUtils.AllGroupName] = new List<int>(orderHint);

        var normalizedAll = orderHint.Where(idx => !_globalDrawn.Contains(idx)).ToList();
        var seenAll = new HashSet<int>(normalizedAll);
        foreach (var idx in baseAllList)
        {
            if (seenAll.Contains(idx) || _globalDrawn.Contains(idx))
            {
                continue;
            }
            normalizedAll.Add(idx);
            seenAll.Add(idx);
        }
        _groupRemaining[IdentityUtils.AllGroupName] = normalizedAll;
    }

    private void MarkStudentDrawn(int studentIndex)
    {
        if (!_studentGroups.TryGetValue(studentIndex, out var groups))
        {
            return;
        }
        _globalDrawn.Add(studentIndex);
        foreach (var group in groups)
        {
            var history = group.Equals(IdentityUtils.AllGroupName, StringComparison.OrdinalIgnoreCase)
                ? _groupDrawnHistory.GetValueOrDefault(IdentityUtils.AllGroupName) ?? _globalDrawn
                : _groupDrawnHistory.GetValueOrDefault(group) ?? new HashSet<int>();
            history.Add(studentIndex);
            _groupDrawnHistory[group] = history;
            if (_groupRemaining.TryGetValue(group, out var pool) && pool.Count > 0)
            {
                pool.RemoveAll(idx => idx == studentIndex);
            }
        }
        RefreshAllGroupPool();
    }

    private void RemoveFromGlobalHistory(int studentIndex, string? ignoreGroup)
    {
        foreach (var pair in _groupDrawnHistory)
        {
            if (pair.Key.Equals(IdentityUtils.AllGroupName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
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

    private string ToSaveKey(int index)
    {
        if (index < 0 || index >= _roster.Students.Count)
        {
            return string.Empty;
        }
        var student = _roster.Students[index];
        if (!string.IsNullOrWhiteSpace(student.RowKey)
            && !_duplicateRowKeys.Contains(student.RowKey))
        {
            return student.RowKey;
        }
        return student.RowId;
    }

    private HashSet<string> BuildDuplicateRowKeys()
    {
        var duplicates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var student in _roster.Students)
        {
            if (string.IsNullOrWhiteSpace(student.RowKey))
            {
                continue;
            }
            if (!seen.Add(student.RowKey))
            {
                duplicates.Add(student.RowKey);
            }
        }
        return duplicates;
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
        var duplicateRowIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < _roster.Students.Count; i++)
        {
            var student = _roster.Students[i];
            if (!string.IsNullOrWhiteSpace(student.RowId))
            {
                if (rowIdMap.ContainsKey(student.RowId))
                {
                    duplicateRowIds.Add(student.RowId);
                }
                else
                {
                    rowIdMap[student.RowId] = i;
                }
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
        foreach (var rowId in duplicateRowIds)
        {
            rowIdMap.Remove(rowId);
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

    private List<int> ResolveIndexList(IEnumerable<string>? keys, (Dictionary<string, int> RowIdMap, Dictionary<string, int> RowKeyMap) lookup)
    {
        var list = new List<int>();
        if (keys == null)
        {
            return list;
        }
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
