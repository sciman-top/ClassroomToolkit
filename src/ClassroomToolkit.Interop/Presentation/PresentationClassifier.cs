namespace ClassroomToolkit.Interop.Presentation;

public sealed class PresentationClassifier
{
    private static readonly string[] DefaultWpsSlideshowTokens =
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

    private static readonly string[] DefaultOfficeClassTokens =
    {
        "screenclass",
        "pptviewwndclass",
        "powerpntframeclass",
        "powerpointframeclass",
        "pptframeclass"
    };

    private static readonly string[] DefaultSlideshowClassTokens =
    {
        "screenclass",
        "pptviewwndclass"
    };

    private readonly HashSet<string> _wpsSlideshowTokens;
    private readonly HashSet<string> _officeClassTokens;
    private readonly HashSet<string> _slideshowClassTokens;
    private readonly HashSet<string> _wpsProcessTokens;
    private readonly HashSet<string> _officeProcessTokens;

    public PresentationClassifier()
        : this(PresentationClassifierOverrides.Empty)
    {
    }

    public PresentationClassifier(PresentationClassifierOverrides? overrides)
    {
        var effectiveOverrides = overrides ?? PresentationClassifierOverrides.Empty;
        _wpsSlideshowTokens = BuildTokenSet(DefaultWpsSlideshowTokens, effectiveOverrides.AdditionalWpsClassTokens);
        _officeClassTokens = BuildTokenSet(DefaultOfficeClassTokens, effectiveOverrides.AdditionalOfficeClassTokens);
        _slideshowClassTokens = BuildTokenSet(DefaultSlideshowClassTokens, effectiveOverrides.AdditionalSlideshowClassTokens);
        _wpsProcessTokens = BuildTokenSet(Array.Empty<string>(), effectiveOverrides.AdditionalWpsProcessTokens);
        _officeProcessTokens = BuildTokenSet(Array.Empty<string>(), effectiveOverrides.AdditionalOfficeProcessTokens);
    }

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

        var normalizedClassNames = classNames
            .Select(Normalize)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .ToArray();
        var hasScreenClass = normalizedClassNames.Any(static name =>
            string.Equals(name, "screenclass", StringComparison.OrdinalIgnoreCase));
        var hasOfficeSpecificClass = normalizedClassNames.Any(name =>
            !string.Equals(name, "screenclass", StringComparison.OrdinalIgnoreCase)
            && _officeClassTokens.Contains(name));
        if (hasOfficeSpecificClass)
        {
            return PresentationType.Office;
        }
        if (hasScreenClass)
        {
            if (IsWpsLikeProcess(process) && !IsOfficeProcess(process))
            {
                return PresentationType.Wps;
            }
            if (IsOfficeProcess(process))
            {
                return PresentationType.Office;
            }
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

    public bool IsSlideshowWindow(PresentationWindowInfo info)
    {
        if (info == null)
        {
            return false;
        }
        var process = Normalize(info.ProcessName);
        var classNames = info.ClassNames ?? Array.Empty<string>();
        if (classNames.Any(name => HasWpsPresentationSignature(name)))
        {
            return true;
        }
        if (classNames.Any(name => _slideshowClassTokens.Contains(Normalize(name))))
        {
            return IsOfficeProcess(process) || IsWpsLikeProcess(process);
        }
        return false;
    }

    private bool HasWpsPresentationSignature(string className)
    {
        var normalized = Normalize(className);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }
        if (_wpsSlideshowTokens.Contains(normalized))
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

    private bool IsWpsProcess(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }
        if (MatchesAnyProcessToken(processName, _wpsProcessTokens))
        {
            return true;
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

    private bool IsWpsLikeProcess(string processName)
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

    private bool IsOfficeProcess(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }
        if (MatchesAnyProcessToken(processName, _officeProcessTokens))
        {
            return true;
        }
        return processName.Contains("powerpnt", StringComparison.OrdinalIgnoreCase)
               || processName.StartsWith("pptview", StringComparison.OrdinalIgnoreCase);
    }

    private static HashSet<string> BuildTokenSet(
        IReadOnlyList<string> defaults,
        IReadOnlyList<string> additional)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddTokens(set, defaults);
        AddTokens(set, additional);
        return set;
    }

    private static void AddTokens(HashSet<string> set, IReadOnlyList<string> values)
    {
        if (values == null || values.Count == 0)
        {
            return;
        }

        for (var i = 0; i < values.Count; i++)
        {
            var normalized = Normalize(values[i]);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }

            set.Add(normalized);
        }
    }

    private static bool MatchesAnyProcessToken(string processName, IEnumerable<string> tokens)
    {
        foreach (var token in tokens)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            if (processName.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
