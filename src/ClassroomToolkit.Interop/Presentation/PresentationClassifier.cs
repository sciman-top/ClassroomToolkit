namespace ClassroomToolkit.Interop.Presentation;

public sealed class PresentationClassifier
{
    private static readonly HashSet<string> WpsClassTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "kwppshowframeclass",
        "kwppshowframe",
        "kwppshowwndclass",
        "kwpsshowframe",
        "kwpsshowframeclass",
        "wpsshowframe",
        "wpsshowframeclass",
        "wpsshowwndclass"
    };

    private static readonly HashSet<string> OfficeClassTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "screenclass",
        "pptviewwndclass",
        "powerpntframeclass",
        "powerpointframeclass",
        "pptframeclass"
    };

    public PresentationType Classify(PresentationWindowInfo info)
    {
        if (info == null)
        {
            return PresentationType.None;
        }
        var process = Normalize(info.ProcessName);
        var classNames = info.ClassNames ?? Array.Empty<string>();

        if (classNames.Any(name => WpsClassTokens.Contains(Normalize(name))))
        {
            return PresentationType.Wps;
        }
        if (classNames.Any(name => OfficeClassTokens.Contains(Normalize(name))))
        {
            return PresentationType.Office;
        }

        if (IsWpsProcess(process))
        {
            return PresentationType.Wps;
        }
        if (IsOfficeProcess(process))
        {
            return PresentationType.Office;
        }
        return PresentationType.Other;
    }

    private static bool IsWpsProcess(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }
        if (processName.StartsWith("wpp", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (processName.StartsWith("wppt", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        return processName.Contains("wpspresentation", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOfficeProcess(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }
        return processName.Contains("powerpnt", StringComparison.OrdinalIgnoreCase)
               || processName.StartsWith("pptview", StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
