namespace ClassroomToolkit.Interop.Presentation;

public static class PresentationProcessSignaturePolicy
{
    public static string NormalizeProcessName(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return string.Empty;
        }

        var normalized = processName.Trim();
        if (normalized.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^4];
        }

        return normalized.ToLowerInvariant();
    }

    public static bool IsOfficeProcessName(string? processName)
    {
        var normalized = NormalizeProcessName(processName);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        return normalized.Contains("powerpnt", StringComparison.OrdinalIgnoreCase)
               || normalized.Contains("powerpoint", StringComparison.OrdinalIgnoreCase)
               || normalized.StartsWith("pptview", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsWpsProcessName(string? processName)
    {
        var normalized = NormalizeProcessName(processName);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        return normalized.StartsWith("wpp", StringComparison.OrdinalIgnoreCase)
               || normalized.StartsWith("wppt", StringComparison.OrdinalIgnoreCase)
               || normalized.Contains("kwpp", StringComparison.OrdinalIgnoreCase)
               || normalized.Contains("kwps", StringComparison.OrdinalIgnoreCase)
               || normalized.Contains("wpspresentation", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsWpsLikeProcessName(string? processName)
    {
        var normalized = NormalizeProcessName(processName);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        return IsWpsProcessName(normalized)
               || normalized.StartsWith("wps", StringComparison.OrdinalIgnoreCase);
    }

    public static bool MatchesAnyProcessToken(
        string? processName,
        IReadOnlyCollection<string> tokens)
    {
        var normalized = NormalizeProcessName(processName);
        if (string.IsNullOrWhiteSpace(normalized)
            || tokens == null
            || tokens.Count == 0)
        {
            return false;
        }

        foreach (var token in tokens)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            if (normalized.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
