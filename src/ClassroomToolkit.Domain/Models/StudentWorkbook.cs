using System.Collections.ObjectModel;

namespace ClassroomToolkit.Domain.Models;

public sealed class StudentWorkbook
{
    private readonly Dictionary<string, ClassRoster> _classes;

    public StudentWorkbook(IDictionary<string, ClassRoster> classes, string? activeClass)
    {
        _classes = new Dictionary<string, ClassRoster>(StringComparer.OrdinalIgnoreCase);
        if (classes != null)
        {
            foreach (var pair in classes)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                {
                    continue;
                }
                _classes[pair.Key.Trim()] = pair.Value;
            }
        }
        if (_classes.Count == 0)
        {
            _classes["班级1"] = new ClassRoster("班级1", Array.Empty<StudentRecord>());
        }
        ActiveClass = ResolveActiveClass(activeClass);
    }

    public string ActiveClass { get; private set; }

    public IReadOnlyDictionary<string, ClassRoster> Classes => new ReadOnlyDictionary<string, ClassRoster>(_classes);

    public IReadOnlyList<string> ClassNames => _classes.Keys.ToList();

    public ClassRoster GetActiveRoster()
    {
        return _classes[ActiveClass];
    }

    public void SetActiveClass(string? className)
    {
        ActiveClass = ResolveActiveClass(className);
    }

    public void UpdateClass(string className, ClassRoster roster)
    {
        if (string.IsNullOrWhiteSpace(className))
        {
            return;
        }
        _classes[className.Trim()] = roster;
        ActiveClass = ResolveActiveClass(ActiveClass);
    }

    private string ResolveActiveClass(string? requested)
    {
        if (!string.IsNullOrWhiteSpace(requested) && _classes.ContainsKey(requested.Trim()))
        {
            return requested.Trim();
        }
        return _classes.Keys.First();
    }
}
