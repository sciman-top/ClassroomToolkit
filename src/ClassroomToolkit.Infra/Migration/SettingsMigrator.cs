namespace ClassroomToolkit.Infra.Migration;

public static class SettingsMigrator
{
    public const string CurrentVersion = "2.0";
    public const string MetaSection = "_meta";
    public const string VersionKey = "_settings_version";

    public static bool RequiresMigration(Dictionary<string, Dictionary<string, string>> data)
    {
        ArgumentNullException.ThrowIfNull(data);

        return !data.TryGetValue(MetaSection, out var meta)
            || !meta.TryGetValue(VersionKey, out var version)
            || !string.Equals(version, CurrentVersion, StringComparison.OrdinalIgnoreCase);
    }

    public static Dictionary<string, Dictionary<string, string>> Migrate(
        Dictionary<string, Dictionary<string, string>> data)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (!data.TryGetValue(MetaSection, out var meta))
        {
            meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            data[MetaSection] = meta;
        }
        var existingVersion = meta.TryGetValue(VersionKey, out var version) ? version : string.Empty;
        if (string.Equals(existingVersion, CurrentVersion, StringComparison.OrdinalIgnoreCase))
        {
            return data;
        }

        NormalizePresentationInputModes(data);
        meta[VersionKey] = CurrentVersion;
        return data;
    }

    private static void NormalizePresentationInputModes(Dictionary<string, Dictionary<string, string>> data)
    {
        if (!data.TryGetValue("Paint", out var paint))
        {
            return;
        }
        var mode = paint.TryGetValue("wps_input_mode", out var value) ? value : string.Empty;
        var rawInput = paint.TryGetValue("wps_raw_input", out var rawValue)
            && (rawValue.Trim().Equals("true", StringComparison.OrdinalIgnoreCase)
                || rawValue.Trim().Equals("1", StringComparison.OrdinalIgnoreCase));

        var normalized = NormalizeWpsMode(mode, rawInput);
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            paint["wps_input_mode"] = normalized;
        }

        var officeMode = paint.TryGetValue("office_input_mode", out var officeValue)
            ? officeValue
            : string.Empty;
        if (string.IsNullOrWhiteSpace(officeMode))
        {
            // Legacy compatibility: office mode did not exist previously.
            // Keep inherited raw/auto, but avoid message default for Office.
            paint["office_input_mode"] = string.Equals(normalized, "message", StringComparison.OrdinalIgnoreCase)
                ? "auto"
                : normalized;
            return;
        }

        paint["office_input_mode"] = NormalizeWpsMode(officeMode, rawInput: false);
    }

    private static string NormalizeWpsMode(string? mode, bool rawInput)
    {
        if (string.IsNullOrWhiteSpace(mode))
        {
            return rawInput ? "raw" : "auto";
        }
        var normalized = mode.Trim().ToUpperInvariant();
        return normalized switch
        {
            "AUTO" => "auto",
            "RAW" => "raw",
            "MESSAGE" => "message",
            "MANUAL" => rawInput ? "raw" : "message",
            _ => rawInput ? "raw" : "auto"
        };
    }

}
