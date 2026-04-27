using System.Diagnostics.CodeAnalysis;

namespace ClassroomToolkit.Domain.Models;

[SuppressMessage(
    "Design",
    "CA1002:Do not expose generic lists",
    Justification = "Mutable collection properties are the persisted roll-state JSON contract.")]
[SuppressMessage(
    "Usage",
    "CA2227:Collection properties should be read only",
    Justification = "Setters are required for JSON deserialization and legacy persisted-state compatibility.")]
public sealed class ClassRollState
{
    public string Version { get; set; } = "2.0";
    public string CurrentGroup { get; set; } = "全部";
    public Dictionary<string, List<string>> GroupRemaining { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string?> GroupLast { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> GlobalDrawn { get; set; } = new();
    public string? CurrentStudent { get; set; }
    public string? PendingStudent { get; set; }
}
