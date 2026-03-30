using System.Text.Json;

namespace ClassroomToolkit.Interop.Presentation;

public readonly record struct PresentationClassifierLearnRequest(
    PresentationType Type,
    IReadOnlyList<string> ClassTokens,
    string ProcessToken);

public static class PresentationClassifierAutoLearnPolicy
{
    public static bool TryBuildRequest(
        PresentationWindowInfo info,
        PresentationType type,
        out PresentationClassifierLearnRequest request)
    {
        request = default;
        if (info == null)
        {
            return false;
        }
        if (type is not (PresentationType.Wps or PresentationType.Office))
        {
            return false;
        }

        var classTokens = (info.ClassNames ?? Array.Empty<string>())
            .Select(static name => name?.Trim())
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Select(static name => name!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToArray();
        var processToken = NormalizeProcessToken(info.ProcessName);
        if (classTokens.Length == 0 && string.IsNullOrWhiteSpace(processToken))
        {
            return false;
        }

        request = new PresentationClassifierLearnRequest(type, classTokens, processToken);
        return true;
    }

    public static bool TryMergeOverridesJson(
        string? existingJson,
        PresentationClassifierLearnRequest request,
        out string mergedJson,
        out string error)
    {
        mergedJson = existingJson ?? string.Empty;
        error = string.Empty;

        if (!PresentationClassifierOverridesParser.TryParse(
                existingJson,
                out var overrides,
                out var parseError))
        {
            error = $"overrides-parse-failed: {parseError}";
            return false;
        }
        if (!PresentationClassifierOverridesParser.TryParseScoringOptions(
                existingJson,
                out var scoring,
                out var scoringError))
        {
            error = $"scoring-parse-failed: {scoringError}";
            return false;
        }

        var additionalWpsClassTokens = new List<string>(overrides.AdditionalWpsClassTokens);
        var additionalOfficeClassTokens = new List<string>(overrides.AdditionalOfficeClassTokens);
        var additionalSlideshowClassTokens = new List<string>(overrides.AdditionalSlideshowClassTokens);
        var additionalWpsProcessTokens = new List<string>(overrides.AdditionalWpsProcessTokens);
        var additionalOfficeProcessTokens = new List<string>(overrides.AdditionalOfficeProcessTokens);

        var changed = false;
        for (var i = 0; i < request.ClassTokens.Count; i++)
        {
            var token = request.ClassTokens[i];
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            changed |= AddDistinct(additionalSlideshowClassTokens, token);
            if (request.Type == PresentationType.Wps)
            {
                changed |= AddDistinct(additionalWpsClassTokens, token);
            }
            else
            {
                changed |= AddDistinct(additionalOfficeClassTokens, token);
            }
        }

        if (!string.IsNullOrWhiteSpace(request.ProcessToken))
        {
            if (request.Type == PresentationType.Wps)
            {
                changed |= AddDistinct(additionalWpsProcessTokens, request.ProcessToken);
            }
            else
            {
                changed |= AddDistinct(additionalOfficeProcessTokens, request.ProcessToken);
            }
        }

        if (!changed)
        {
            return true;
        }

        var model = new PersistedOverridesModel
        {
            AdditionalWpsClassTokens = additionalWpsClassTokens,
            AdditionalOfficeClassTokens = additionalOfficeClassTokens,
            AdditionalSlideshowClassTokens = additionalSlideshowClassTokens,
            AdditionalWpsProcessTokens = additionalWpsProcessTokens,
            AdditionalOfficeProcessTokens = additionalOfficeProcessTokens,
            ClassMatchWeight = scoring.ClassMatchWeight,
            ProcessMatchWeight = scoring.ProcessMatchWeight,
            NoCaptionWeight = scoring.NoCaptionWeight,
            IsFullscreenWeight = scoring.IsFullscreenWeight,
            FullscreenClassMatchBonus = scoring.FullscreenClassMatchBonus,
            RequireClassMatchOrFullscreen = scoring.RequireClassMatchOrFullscreen,
            MinimumCandidateScore = scoring.MinimumCandidateScore
        };

        mergedJson = JsonSerializer.Serialize(model);
        return true;
    }

    private static string NormalizeProcessToken(string? processName)
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

        return normalized;
    }

    private static bool AddDistinct(ICollection<string> list, string token)
    {
        if (list.Any(existing => string.Equals(existing, token, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        list.Add(token);
        return true;
    }

    private sealed class PersistedOverridesModel
    {
        public List<string> AdditionalWpsClassTokens { get; set; } = new();

        public List<string> AdditionalOfficeClassTokens { get; set; } = new();

        public List<string> AdditionalSlideshowClassTokens { get; set; } = new();

        public List<string> AdditionalWpsProcessTokens { get; set; } = new();

        public List<string> AdditionalOfficeProcessTokens { get; set; } = new();

        public int ClassMatchWeight { get; set; }

        public int ProcessMatchWeight { get; set; }

        public int NoCaptionWeight { get; set; }

        public int IsFullscreenWeight { get; set; }

        public int FullscreenClassMatchBonus { get; set; }

        public bool RequireClassMatchOrFullscreen { get; set; }

        public int MinimumCandidateScore { get; set; }
    }
}
