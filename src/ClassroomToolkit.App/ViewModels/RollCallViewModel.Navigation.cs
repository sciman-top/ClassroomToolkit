using ClassroomToolkit.App.Models;
using ClassroomToolkit.App;
using ClassroomToolkit.Application.UseCases.RollCall;
using ClassroomToolkit.Domain.Models;
using ClassroomToolkit.Domain.Utilities;

namespace ClassroomToolkit.App.ViewModels;

public sealed partial class RollCallViewModel
{
    public bool SwitchClass(string className, bool updatePhoto = true)
    {
        if (_engine == null || _workbook == null) return false;
        if (string.IsNullOrWhiteSpace(className)) return false;

        var trimmed = className.Trim();
        if (!_workbook.ClassNames.Any(name => name.Equals(trimmed, StringComparison.OrdinalIgnoreCase))) return false;
        if (trimmed.Equals(_workbook.ActiveClass, StringComparison.OrdinalIgnoreCase)) return false;

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
        if (_engine == null) { message = "暂无学生数据，无法显示名单。"; return false; }

        var roster = _engine.Roster;
        if (roster.Students.Count == 0) { message = "暂无学生数据，无法显示名单。"; return false; }

        var groupKey = ResolveGroupKey(CurrentGroup);
        if (!_engine.GroupAll.TryGetValue(groupKey, out var indices))
        {
            _engine.GroupAll.TryGetValue(IdentityUtils.AllGroupName, out indices);
        }
        indices ??= new List<int>();

        if (indices.Count == 0)
        {
            message = CurrentGroup == IdentityUtils.AllGroupName ? "当前没有可显示的学生名单。" : "当前分组没有可显示的学生名单。";
            return false;
        }

        var remaining = _engine.GroupRemaining.TryGetValue(groupKey, out var pool) ? new HashSet<int>(pool) : new HashSet<int>();
        var list = new List<(StudentSortKey Key, StudentListItem Item)>();

        foreach (var idx in indices)
        {
            if (idx < 0 || idx >= roster.Students.Count) continue;
            var student = roster.Students[idx];
            var displayId = IdentityUtils.CompactText(student.StudentId);
            var displayName = IdentityUtils.NormalizeText(student.Name);
            var key = BuildStudentSortKey(displayId, displayName);
            var item = new StudentListItem(displayId, displayName, idx, !remaining.Contains(idx));
            list.Add((key, item));
        }

        if (list.Count == 0) { message = "当前没有可显示的学生名单。"; return false; }

        list.Sort((a, b) => a.Key.CompareTo(b.Key));
        students = list.Select(item => item.Item).ToList();
        return true;
    }

    private string ResolveGroupKey(string group)
    {
        if (_engine == null) return group;
        var normalized = IdentityUtils.NormalizeGroupName(group);
        if (_engine.GroupAll.ContainsKey(normalized)) return normalized;

        var trimmed = normalized.Trim();
        if (trimmed.EndsWith("组", StringComparison.Ordinal))
        {
            var stripped = trimmed[..^1];
            if (_engine.GroupAll.ContainsKey(stripped)) return stripped;
        }
        else
        {
            var withSuffix = $"{trimmed}组";
            if (_engine.GroupAll.ContainsKey(withSuffix)) return withSuffix;
        }

        foreach (var key in _engine.GroupAll.Keys)
        {
            if (NormalizeGroupKey(key) == NormalizeGroupKey(trimmed)) return key;
        }
        return normalized;
    }

    private static string NormalizeGroupKey(string group)
    {
        var normalized = IdentityUtils.NormalizeGroupName(group);
        if (normalized.EndsWith("组", StringComparison.Ordinal)) normalized = normalized[..^1];
        return normalized;
    }

    public bool SetCurrentStudentByIndex(int index)
    {
        if (_engine == null) return false;
        if (index < 0 || index >= _engine.Roster.Students.Count) return false;
        _engine.SetCurrentStudentIndex(index);
        UpdateCurrentStudent();
        return true;
    }

    public bool TryRollNext(out string? message)
    {
        message = null;
        if (_engine == null) { message = "暂无学生数据，无法点名。"; SetPlaceholderStudent(); return false; }
        if (_engine.Roster.Students.Count == 0) { message = "当前没有可点名的学生。"; SetPlaceholderStudent(); return false; }

        var groupKey = ResolveGroupKey(CurrentGroup);
        if (!_engine.GroupRemaining.TryGetValue(groupKey, out var remaining) || remaining.Count == 0)
        {
            if (!_engine.GroupAll.TryGetValue(groupKey, out var baseList) || baseList.Count == 0)
            {
                message = $"'{CurrentGroup}' 分组当前没有可点名的学生。";
                SetPlaceholderStudent();
                return false;
            }
            message = _engine.AllGroupsCompleted() ? "所有学生都已完成点名，请点击“重置”按钮重新开始。" : $"'{CurrentGroup}' 的同学已经全部点到，请切换其他分组或点击“重置”按钮。";
            return false;
        }

        var student = _engine.RollNext();
        if (student == null) { SetPlaceholderStudent(); return false; }

        CurrentStudentId = IdentityUtils.CompactText(student.StudentId);
        CurrentStudentName = IdentityUtils.NormalizeText(student.Name);
        return true;
    }

    private void UpdateGroupSelection()
    {
        foreach (var btn in GroupButtons)
        {
            if (!btn.IsReset)
            {
                btn.IsSelected = string.Equals(btn.Label, _currentGroup, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    public void ResetCurrentGroup()
    {
        if (_engine == null) return;
        var groupKey = ResolveGroupKey(CurrentGroup);
        _engine.ResetGroup(groupKey);
        UpdateCurrentStudent();
    }

    public void SetCurrentGroup(string group)
    {
        if (_engine == null) return;
        var groupKey = ResolveGroupKey(group);
        _engine.SetCurrentGroup(groupKey);
        CurrentGroup = _engine.CurrentGroup;
        UpdateCurrentStudent();
    }

    public void SwitchToNextGroup()
    {
        if (_engine == null || Groups.Count <= 1) return;
        var currentIdx = Groups.IndexOf(CurrentGroup);
        var nextIdx = (currentIdx + 1) % Groups.Count;
        SetCurrentGroup(Groups[nextIdx]);
    }

    private void RefreshGroups()
    {
        if (_engine == null) return;
        Groups.Clear();
        GroupButtons.Clear();
        foreach (var group in _engine.Roster.Groups)
        {
            Groups.Add(group);
            GroupButtons.Add(new GroupButtonItem(group, false) { IsSelected = string.Equals(group, _currentGroup, StringComparison.OrdinalIgnoreCase) });
        }
        GroupButtons.Add(new GroupButtonItem("重置", true));
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
    }

    private void SetPlaceholderStudent()
    {
        CurrentStudentId = ShowId ? "学号" : string.Empty;
        CurrentStudentName = ShowName ? "姓名" : string.Empty;
        CurrentStudentPhotoPath = null;
    }

    public void SaveState()
    {
        if (_workbook == null || !_canPersistWorkbook) return;
        StoreCurrentState();

        try
        {
            _workbookUseCase.Save(_dataPath, _workbook, _classStates);
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            DataSaveFailed?.Invoke($"保存状态失败: {ex.Message}");
        }
    }

    private void StoreCurrentState()
    {
        if (_engine != null && !string.IsNullOrWhiteSpace(ActiveClassName))
        {
            _classStates[ActiveClassName] = _engine.CaptureState();
        }
    }

    private StudentSortKey BuildStudentSortKey(string studentId, string name)
    {
        return new StudentSortKey(studentId, name);
    }

    private sealed record StudentSortKey(string StudentId, string Name) : IComparable<StudentSortKey>
    {
        public int CompareTo(StudentSortKey? other)
        {
            if (other == null) return 1;
            if (long.TryParse(StudentId, out var thisId) && long.TryParse(other.StudentId, out var otherId))
            {
                var cmp = thisId.CompareTo(otherId);
                if (cmp != 0) return cmp;
            }
            var idCmp = string.Compare(StudentId, other.StudentId, StringComparison.Ordinal);
            return idCmp != 0 ? idCmp : string.Compare(Name, other.Name, StringComparison.Ordinal);
        }
    }
}
