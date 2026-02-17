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
        var payload = CloneWithVersion(state, CurrentVersion);
        return JsonSerializer.Serialize(payload, Options);
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
        catch (JsonException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    public static string SerializeWorkbookStates(Dictionary<string, ClassRollState> states)
    {
        var payload = states ?? new Dictionary<string, ClassRollState>(StringComparer.OrdinalIgnoreCase);
        var snapshot = new Dictionary<string, ClassRollState>(payload.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in payload)
        {
            snapshot[pair.Key] = CloneWithVersion(pair.Value, CurrentVersion);
        }
        return JsonSerializer.Serialize(snapshot, Options);
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
        catch (JsonException)
        {
            return new Dictionary<string, ClassRollState>(StringComparer.OrdinalIgnoreCase);
        }
        catch (NotSupportedException)
        {
            return new Dictionary<string, ClassRollState>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static ClassRollState CloneWithVersion(ClassRollState state, string version)
    {
        var cloned = new ClassRollState
        {
            Version = version,
            CurrentGroup = state.CurrentGroup,
            CurrentStudent = state.CurrentStudent,
            PendingStudent = state.PendingStudent
        };
        foreach (var pair in state.GroupRemaining)
        {
            cloned.GroupRemaining[pair.Key] = new List<string>(pair.Value);
        }
        foreach (var pair in state.GroupLast)
        {
            cloned.GroupLast[pair.Key] = pair.Value;
        }
        cloned.GlobalDrawn = new List<string>(state.GlobalDrawn);
        return cloned;
    }
}
