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
        string rowKey)
    {
        StudentId = studentId;
        Name = name;
        ClassName = className;
        GroupName = groupName;
        RowId = rowId;
        RowKey = rowKey;
    }

    public string StudentId { get; }
    public string Name { get; }
    public string ClassName { get; }
    public string GroupName { get; }
    public string RowId { get; }
    public string RowKey { get; }

    public static StudentRecord Create(
        string studentId,
        string name,
        string className,
        string groupName,
        string? rowId = null)
    {
        var normalizedId = IdentityUtils.CompactText(studentId);
        var normalizedName = IdentityUtils.NormalizeText(name);
        var normalizedClass = IdentityUtils.NormalizeText(className);
        var normalizedGroup = IdentityUtils.NormalizeGroupName(groupName);
        var resolvedRowId = string.IsNullOrWhiteSpace(rowId) ? Guid.NewGuid().ToString("N") : rowId.Trim();
        var rowKey = IdentityUtils.BuildRowKey(normalizedId, normalizedName, normalizedClass, normalizedGroup);
        return new StudentRecord(normalizedId, normalizedName, normalizedClass, normalizedGroup, resolvedRowId, rowKey);
    }
}
