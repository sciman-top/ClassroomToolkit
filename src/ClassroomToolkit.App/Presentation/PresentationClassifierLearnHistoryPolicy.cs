using System.Text.Json;

namespace ClassroomToolkit.App.Presentation;

internal sealed record PresentationClassifierLearnRecord(string Utc, string Detail);

internal static class PresentationClassifierLearnHistoryPolicy
{
    private const int MaxRecords = 5;

    public static string Append(string? existingJson, DateTime utc, string detail)
    {
        var records = Parse(existingJson).ToList();
        var normalizedDetail = string.IsNullOrWhiteSpace(detail) ? "unknown" : detail.Trim();
        records.Add(new PresentationClassifierLearnRecord(
            utc.ToUniversalTime().ToString("O"),
            normalizedDetail));

        records = records
            .DistinctBy(static item => $"{item.Utc}|{item.Detail}", StringComparer.OrdinalIgnoreCase)
            .TakeLast(MaxRecords)
            .ToList();
        return JsonSerializer.Serialize(records);
    }

    public static IReadOnlyList<PresentationClassifierLearnRecord> Parse(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return Array.Empty<PresentationClassifierLearnRecord>();
        }

        try
        {
            var data = JsonSerializer.Deserialize<List<PresentationClassifierLearnRecord>>(rawJson);
            if (data == null || data.Count == 0)
            {
                return Array.Empty<PresentationClassifierLearnRecord>();
            }

            return data
                .Where(static item => !string.IsNullOrWhiteSpace(item.Utc) && !string.IsNullOrWhiteSpace(item.Detail))
                .ToArray();
        }
        catch (JsonException)
        {
            return Array.Empty<PresentationClassifierLearnRecord>();
        }
        catch (NotSupportedException)
        {
            return Array.Empty<PresentationClassifierLearnRecord>();
        }
    }
}
