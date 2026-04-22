using ClassroomToolkit.Domain.Utilities;

namespace ClassroomToolkit.Domain.Models;

public sealed class ClassRoster
{
    public static readonly string[] DefaultColumns = { "学号", "姓名", "班级", "分组" };
    public const string InternalRowIdColumn = "__row_id__";

    private readonly List<StudentRecord> _students;
    private readonly Dictionary<string, List<int>> _groupIndexMap;

    public ClassRoster(string className, IEnumerable<StudentRecord> students, IReadOnlyList<string>? columnOrder = null)
    {
        ClassName = IdentityUtils.NormalizeText(className);
        _students = students?.ToList() ?? new List<StudentRecord>();
        _groupIndexMap = BuildGroupIndexMap(_students);
        Groups = BuildGroupList(_groupIndexMap.Keys);
        ColumnOrder = BuildColumnOrder(columnOrder);
    }

    public string ClassName { get; }

    public IReadOnlyList<StudentRecord> Students => _students;

    public IReadOnlyList<string> Groups { get; }

    public IReadOnlyDictionary<string, List<int>> GroupIndexMap => _groupIndexMap;

    public IReadOnlyList<string> ColumnOrder { get; }

    private static Dictionary<string, List<int>> BuildGroupIndexMap(List<StudentRecord> students)
    {
        var map = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < students.Count; i++)
        {
            var group = IdentityUtils.NormalizeGroupName(students[i].GroupName);
            if (string.IsNullOrWhiteSpace(group))
            {
                group = string.Empty;
            }
            if (!map.TryGetValue(group, out var list))
            {
                list = new List<int>();
                map[group] = list;
            }
            list.Add(i);
        }
        // "全部" is a reserved aggregate group and must always represent all students.
        map[IdentityUtils.AllGroupName] = Enumerable.Range(0, students.Count).ToList();
        return map;
    }

    private static List<string> BuildGroupList(IEnumerable<string> groups)
    {
        var list = new List<string> { IdentityUtils.AllGroupName };
        var unique = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in groups)
        {
            if (string.IsNullOrWhiteSpace(group))
            {
                continue;
            }
            if (group.Equals(IdentityUtils.AllGroupName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            if (!unique.ContainsKey(group))
            {
                unique[group] = group;
            }
        }
        var sorted = unique.Values.ToList();
        sorted.Sort(StringComparer.Ordinal);
        list.AddRange(sorted);
        return list;
    }

    private static List<string> BuildColumnOrder(IReadOnlyList<string>? columnOrder)
    {
        var normalized = new List<string>();
        if (columnOrder != null)
        {
            foreach (var column in columnOrder)
            {
                var trimmed = IdentityUtils.NormalizeText(column);
                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    continue;
                }
                if (!normalized.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
                {
                    normalized.Add(trimmed);
                }
            }
        }
        if (normalized.Count == 0)
        {
            normalized.AddRange(DefaultColumns);
        }
        else
        {
            foreach (var column in DefaultColumns)
            {
                if (!normalized.Contains(column, StringComparer.OrdinalIgnoreCase))
                {
                    normalized.Add(column);
                }
            }
        }
        if (!normalized.Contains(InternalRowIdColumn, StringComparer.OrdinalIgnoreCase))
        {
            normalized.Add(InternalRowIdColumn);
        }
        return normalized;
    }
}
