using System;
using System.Collections.Generic;
using System.Linq;
using ClassroomToolkit.Domain.Models;
using ClassroomToolkit.Domain.Utilities;

namespace ClassroomToolkit.Domain.Services;

public sealed partial class RollCallEngine
{
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
