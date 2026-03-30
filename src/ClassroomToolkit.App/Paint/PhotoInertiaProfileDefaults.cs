namespace ClassroomToolkit.App.Paint;

internal static class PhotoInertiaProfileDefaults
{
    internal const string Standard = "standard";
    internal const string Sensitive = "sensitive";
    internal const string Heavy = "heavy";

    internal static string Normalize(string? rawProfile)
    {
        var normalized = (rawProfile ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            Sensitive => Sensitive,
            Heavy => Heavy,
            _ => Standard
        };
    }
}
