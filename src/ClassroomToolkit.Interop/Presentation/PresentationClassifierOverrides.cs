using System.Text.Json;

namespace ClassroomToolkit.Interop.Presentation;

public sealed record PresentationClassifierOverrides(
    IReadOnlyList<string> AdditionalWpsClassTokens,
    IReadOnlyList<string> AdditionalOfficeClassTokens,
    IReadOnlyList<string> AdditionalSlideshowClassTokens,
    IReadOnlyList<string> AdditionalWpsProcessTokens,
    IReadOnlyList<string> AdditionalOfficeProcessTokens)
{
    public static PresentationClassifierOverrides Empty { get; } = new(
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>());
}

public static class PresentationClassifierOverridesParser
{
    public static bool TryParse(
        string? raw,
        out PresentationClassifierOverrides overrides,
        out string error)
    {
        var success = TryParseModel(raw, out var model, out error);
        overrides = success && model != null
            ? new PresentationClassifierOverrides(
                NormalizeTokens(model.AdditionalWpsClassTokens),
                NormalizeTokens(model.AdditionalOfficeClassTokens),
                NormalizeTokens(model.AdditionalSlideshowClassTokens),
                NormalizeTokens(model.AdditionalWpsProcessTokens),
                NormalizeTokens(model.AdditionalOfficeProcessTokens))
            : PresentationClassifierOverrides.Empty;
        return success;
    }

    public static bool TryParseScoringOptions(
        string? raw,
        out PresentationWindowScoringOptions options,
        out string error)
    {
        var success = TryParseModel(raw, out var model, out error);
        if (!success || model == null)
        {
            options = PresentationWindowScoringOptions.Default;
            return success;
        }

        options = new PresentationWindowScoringOptions(
            ClassMatchWeight: ResolveNonNegative(model.ClassMatchWeight, PresentationWindowScoringOptions.Default.ClassMatchWeight),
            ProcessMatchWeight: ResolveNonNegative(model.ProcessMatchWeight, PresentationWindowScoringOptions.Default.ProcessMatchWeight),
            NoCaptionWeight: ResolveNonNegative(model.NoCaptionWeight, PresentationWindowScoringOptions.Default.NoCaptionWeight),
            IsFullscreenWeight: ResolveNonNegative(model.IsFullscreenWeight, PresentationWindowScoringOptions.Default.IsFullscreenWeight),
            FullscreenClassMatchBonus: ResolveNonNegative(model.FullscreenClassMatchBonus, PresentationWindowScoringOptions.Default.FullscreenClassMatchBonus),
            RequireClassMatchOrFullscreen: model.RequireClassMatchOrFullscreen ?? PresentationWindowScoringOptions.Default.RequireClassMatchOrFullscreen,
            MinimumCandidateScore: ResolveNonNegative(model.MinimumCandidateScore, PresentationWindowScoringOptions.Default.MinimumCandidateScore));
        return true;
    }

    private static bool TryParseModel(
        string? raw,
        out PresentationClassifierOverridesDocument? model,
        out string error)
    {
        model = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        try
        {
            model = JsonSerializer.Deserialize<PresentationClassifierOverridesDocument>(raw);
            if (model == null)
            {
                error = "empty-json";
                return false;
            }

            return true;
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }
        catch (NotSupportedException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static string[] NormalizeTokens(List<string>? values)
    {
        if (values == null || values.Count == 0)
        {
            return Array.Empty<string>();
        }

        return values
            .Select(value => value?.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()!;
    }

    private static int ResolveNonNegative(int? value, int fallback)
    {
        if (!value.HasValue)
        {
            return fallback;
        }

        return Math.Max(0, value.Value);
    }
}
