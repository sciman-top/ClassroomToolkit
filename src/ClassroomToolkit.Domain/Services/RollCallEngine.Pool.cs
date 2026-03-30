using System;
using System.Collections.Generic;
using System.Linq;
using ClassroomToolkit.Domain.Models;
using ClassroomToolkit.Domain.Utilities;

namespace ClassroomToolkit.Domain.Services;

public sealed partial class RollCallEngine
{
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
}
