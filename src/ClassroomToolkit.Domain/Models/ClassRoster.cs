using ClassroomToolkit.Domain.Utilities;

namespace ClassroomToolkit.Domain.Models;

public sealed class ClassRoster
{
    private readonly List<StudentRecord> _students;
    private readonly Dictionary<string, List<int>> _groupIndexMap;

    public ClassRoster(string className, IEnumerable<StudentRecord> students)
    {
        ClassName = IdentityUtils.NormalizeText(className);
        _students = students?.ToList() ?? new List<StudentRecord>();
        _groupIndexMap = BuildGroupIndexMap(_students);
        Groups = BuildGroupList(_groupIndexMap.Keys);
    }

    public string ClassName { get; }

    public IReadOnlyList<StudentRecord> Students => _students;

    public IReadOnlyList<string> Groups { get; }

    public IReadOnlyDictionary<string, List<int>> GroupIndexMap => _groupIndexMap;

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
        if (!map.ContainsKey(IdentityUtils.AllGroupName))
        {
            map[IdentityUtils.AllGroupName] = Enumerable.Range(0, students.Count).ToList();
        }
        return map;
    }

    private static IReadOnlyList<string> BuildGroupList(IEnumerable<string> groups)
    {
        var list = new List<string> { IdentityUtils.AllGroupName };
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
            if (!list.Contains(group, StringComparer.OrdinalIgnoreCase))
            {
                list.Add(group);
            }
        }
        return list;
    }
}
