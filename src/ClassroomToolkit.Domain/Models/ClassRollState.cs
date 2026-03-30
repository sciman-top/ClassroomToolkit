namespace ClassroomToolkit.Domain.Models;

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
