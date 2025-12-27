using System.Collections.ObjectModel;
using ClassroomToolkit.Domain.Utilities;

namespace ClassroomToolkit.Domain.Models;

public sealed record StudentRecord
{
    public StudentRecord(
        string studentId,
        string name,
        string className,
        string groupName,
        string rowId,
        string rowKey,
        IReadOnlyDictionary<string, string>? extraFields = null)
    {
        StudentId = studentId;
        Name = name;
        ClassName = className;
        GroupName = groupName;
        RowId = rowId;
        RowKey = rowKey;
        ExtraFields = extraFields == null
            ? new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase))
            : new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(extraFields, StringComparer.OrdinalIgnoreCase));
    }

    public string StudentId { get; }
    public string Name { get; }
    public string ClassName { get; }
    public string GroupName { get; }
    public string RowId { get; }
    public string RowKey { get; }
    public IReadOnlyDictionary<string, string> ExtraFields { get; }

    public static StudentRecord Create(
        string studentId,
        string name,
        string className,
        string groupName,
        string? rowId = null,
        IReadOnlyDictionary<string, string>? extraFields = null)
    {
        var normalizedId = IdentityUtils.CompactText(studentId);
        var normalizedName = IdentityUtils.NormalizeText(name);
        var normalizedClass = IdentityUtils.NormalizeText(className);
        var normalizedGroup = IdentityUtils.NormalizeGroupName(groupName);
        var resolvedRowId = string.IsNullOrWhiteSpace(rowId) ? Guid.NewGuid().ToString("N") : rowId.Trim();
        var rowKey = IdentityUtils.BuildRowKey(normalizedId, normalizedName, normalizedClass, normalizedGroup);
        return new StudentRecord(normalizedId, normalizedName, normalizedClass, normalizedGroup, resolvedRowId, rowKey, extraFields);
    }
}
