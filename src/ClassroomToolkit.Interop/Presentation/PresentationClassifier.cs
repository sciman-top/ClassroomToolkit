namespace ClassroomToolkit.Interop.Presentation;

public sealed class PresentationClassifier
{
    private static readonly HashSet<string> WpsSlideshowTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "kwppshowframeclass",
        "kwppshowframe",
        "kwppshowwndclass",
        "kwpsshowframe",
        "kwpsshowframeclass",
        "kwpsshowwndclass",
        "wpsshowframe",
        "wpsshowframeclass",
        "wpsshowwndclass",
        "kwpsshowwnd",
        "kwppshowwnd",
        "wpsshowwnd"
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

        if (classNames.Any(name => HasWpsPresentationSignature(name)))
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
        if (classNames.Any(name => string.Equals(Normalize(name), "screenclass", StringComparison.OrdinalIgnoreCase))
            && IsWpsLikeProcess(process)
            && !IsOfficeProcess(process))
        {
            return PresentationType.Wps;
        }
        if (IsOfficeProcess(process))
        {
            return PresentationType.Office;
        }
        return PresentationType.Other;
    }

    private static bool HasWpsPresentationSignature(string className)
    {
        var normalized = Normalize(className);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }
        if (WpsSlideshowTokens.Contains(normalized))
        {
            return true;
        }
        if (normalized.StartsWith("kwpp", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("kwpp", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (normalized.StartsWith("kwps", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("kwps", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (normalized.StartsWith("wpp", StringComparison.OrdinalIgnoreCase)
            && !normalized.Contains("wps", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (normalized.StartsWith("wpsshow", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("wpsshow", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        return false;
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
        if (processName.Contains("wpspresentation", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        return false;
    }

    private static bool IsWpsLikeProcess(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }
        if (IsWpsProcess(processName))
        {
            return true;
        }
        return processName.StartsWith("wps", StringComparison.OrdinalIgnoreCase);
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
