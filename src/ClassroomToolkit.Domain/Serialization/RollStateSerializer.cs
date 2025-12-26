using System.Text.Json;
using ClassroomToolkit.Domain.Models;

namespace ClassroomToolkit.Domain.Serialization;

public static class RollStateSerializer
{
    public const string CurrentVersion = "2.0";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static string SerializeClassState(ClassRollState state)
    {
        state.Version = CurrentVersion;
        return JsonSerializer.Serialize(state, Options);
    }

    public static ClassRollState? DeserializeClassState(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }
        try
        {
            return JsonSerializer.Deserialize<ClassRollState>(json, Options);
        }
        catch
        {
            return null;
        }
    }

    public static string SerializeWorkbookStates(Dictionary<string, ClassRollState> states)
    {
        var payload = states ?? new Dictionary<string, ClassRollState>(StringComparer.OrdinalIgnoreCase);
        foreach (var state in payload.Values)
        {
            state.Version = CurrentVersion;
        }
        return JsonSerializer.Serialize(payload, Options);
    }

    public static Dictionary<string, ClassRollState> DeserializeWorkbookStates(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, ClassRollState>(StringComparer.OrdinalIgnoreCase);
        }
        try
        {
            var result = JsonSerializer.Deserialize<Dictionary<string, ClassRollState>>(json, Options);
            return result ?? new Dictionary<string, ClassRollState>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, ClassRollState>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
