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
        ArgumentNullException.ThrowIfNull(state);

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
        var revision = DateTime.UtcNow.Ticks;
        var updatedAtUtc = DateTime.UtcNow;
        return SerializeWorkbookStates(states, revision, updatedAtUtc);
    }

    public static string SerializeWorkbookStates(
        Dictionary<string, ClassRollState> states,
        long revision,
        DateTime updatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(states);

        var payload = states;
        var snapshot = new Dictionary<string, ClassRollState>(payload.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in payload)
        {
            snapshot[pair.Key] = CloneWithVersion(pair.Value, CurrentVersion);
        }

        var envelope = new WorkbookRollStateEnvelope(
            revision,
            updatedAtUtc.ToUniversalTime().ToString("O"),
            snapshot);
        return JsonSerializer.Serialize(envelope, Options);
    }

    public static Dictionary<string, ClassRollState> DeserializeWorkbookStates(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, ClassRollState>(StringComparer.OrdinalIgnoreCase);
        }
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("states", out var statesNode)
                && statesNode.ValueKind == JsonValueKind.Object)
            {
                var states = JsonSerializer.Deserialize<Dictionary<string, ClassRollState>>(
                    statesNode.GetRawText(),
                    Options);
                return states ?? new Dictionary<string, ClassRollState>(StringComparer.OrdinalIgnoreCase);
            }

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

    public static bool TryReadWorkbookMetadata(
        string? json,
        out long? revision,
        out DateTime? updatedAtUtc)
    {
        revision = null;
        updatedAtUtc = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (root.TryGetProperty("revision", out var revisionNode)
                && revisionNode.ValueKind == JsonValueKind.Number
                && revisionNode.TryGetInt64(out var parsedRevision))
            {
                revision = parsedRevision;
            }

            if (root.TryGetProperty("updatedAtUtc", out var updatedAtNode)
                && updatedAtNode.ValueKind == JsonValueKind.String
                && DateTime.TryParse(
                    updatedAtNode.GetString(),
                    null,
                    System.Globalization.DateTimeStyles.RoundtripKind,
                    out var parsedUpdatedAt))
            {
                updatedAtUtc = parsedUpdatedAt.ToUniversalTime();
            }

            return revision.HasValue || updatedAtUtc.HasValue;
        }
        catch (JsonException)
        {
            return false;
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

    private sealed record WorkbookRollStateEnvelope(
        long Revision,
        string UpdatedAtUtc,
        Dictionary<string, ClassRollState> States);
}
