using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ClassroomToolkit.Interop.Presentation;

namespace ClassroomToolkit.Services.Presentation;

public static class PresentationClassifierOverridesPackagePolicy
{
    public const string Schema = "ctoolkit.presentation-overrides-package/v1";
    private static readonly JsonSerializerOptions IndentedJsonSerializerOptions = new()
    {
        WriteIndented = true
    };

    public static bool TryExport(
        string? overridesJson,
        out string packageJson,
        out string error)
    {
        packageJson = string.Empty;
        error = string.Empty;

        if (!TryBuildCanonicalOverridesJson(overridesJson, out var canonicalOverridesJson, out error))
        {
            return false;
        }

        var generatedAtUtc = DateTime.UtcNow.ToString("O");
        var signature = ComputeSignature(canonicalOverridesJson, generatedAtUtc);
        var package = new PackageModel
        {
            Schema = Schema,
            GeneratedAtUtc = generatedAtUtc,
            OverridesJson = canonicalOverridesJson,
            Signature = signature
        };

        packageJson = JsonSerializer.Serialize(package, IndentedJsonSerializerOptions);
        return true;
    }

    public static bool TryImport(
        string? packageJson,
        out string overridesJson,
        out string detail,
        out string error)
    {
        overridesJson = string.Empty;
        detail = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(packageJson))
        {
            error = "package-empty";
            return false;
        }

        PackageModel? package;
        try
        {
            package = JsonSerializer.Deserialize<PackageModel>(packageJson);
        }
        catch (JsonException ex)
        {
            error = $"package-parse-failed:{ex.Message}";
            return false;
        }
        catch (NotSupportedException ex)
        {
            error = $"package-parse-failed:{ex.Message}";
            return false;
        }

        if (package == null)
        {
            error = "package-parse-empty";
            return false;
        }

        if (!string.Equals(package.Schema, Schema, StringComparison.Ordinal))
        {
            error = "package-schema-unsupported";
            return false;
        }

        if (string.IsNullOrWhiteSpace(package.GeneratedAtUtc))
        {
            error = "package-generated-at-missing";
            return false;
        }

        if (!DateTime.TryParse(
                package.GeneratedAtUtc,
                null,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out _))
        {
            error = "package-generated-at-invalid";
            return false;
        }

        if (string.IsNullOrWhiteSpace(package.OverridesJson))
        {
            error = "package-overrides-empty";
            return false;
        }

        if (string.IsNullOrWhiteSpace(package.Signature))
        {
            error = "package-signature-empty";
            return false;
        }

        var expectedSignature = ComputeSignature(package.OverridesJson, package.GeneratedAtUtc);
        if (!string.Equals(package.Signature, expectedSignature, StringComparison.Ordinal))
        {
            error = "package-signature-mismatch";
            return false;
        }

        if (!TryBuildCanonicalOverridesJson(package.OverridesJson, out var canonicalOverridesJson, out var canonicalError))
        {
            error = $"package-overrides-invalid:{canonicalError}";
            return false;
        }

        overridesJson = canonicalOverridesJson;
        if (!PresentationDiagnosticsProbe.TrySummarizeClassifierOverrides(
                canonicalOverridesJson,
                out var classTokenCount,
                out var processTokenCount,
                out var summaryError))
        {
            detail = $"summary-unavailable:{summaryError}";
            return true;
        }

        detail = $"classToken={classTokenCount}; processToken={processTokenCount}";
        return true;
    }

    private static bool TryBuildCanonicalOverridesJson(
        string? rawOverridesJson,
        out string canonicalOverridesJson,
        out string error)
    {
        canonicalOverridesJson = string.Empty;
        error = string.Empty;

        if (!PresentationClassifierOverridesParser.TryParse(
                rawOverridesJson,
                out var overrides,
                out var parseError))
        {
            error = $"overrides-parse-failed:{parseError}";
            return false;
        }

        if (!PresentationClassifierOverridesParser.TryParseScoringOptions(
                rawOverridesJson,
                out var scoring,
                out var scoringError))
        {
            error = $"scoring-parse-failed:{scoringError}";
            return false;
        }

        var canonical = new CanonicalOverridesModel
        {
            AdditionalWpsClassTokens = SortTokens(overrides.AdditionalWpsClassTokens),
            AdditionalOfficeClassTokens = SortTokens(overrides.AdditionalOfficeClassTokens),
            AdditionalSlideshowClassTokens = SortTokens(overrides.AdditionalSlideshowClassTokens),
            AdditionalWpsProcessTokens = SortTokens(overrides.AdditionalWpsProcessTokens),
            AdditionalOfficeProcessTokens = SortTokens(overrides.AdditionalOfficeProcessTokens),
            ClassMatchWeight = scoring.ClassMatchWeight,
            ProcessMatchWeight = scoring.ProcessMatchWeight,
            NoCaptionWeight = scoring.NoCaptionWeight,
            IsFullscreenWeight = scoring.IsFullscreenWeight,
            FullscreenClassMatchBonus = scoring.FullscreenClassMatchBonus,
            RequireClassMatchOrFullscreen = scoring.RequireClassMatchOrFullscreen,
            MinimumCandidateScore = scoring.MinimumCandidateScore
        };

        canonicalOverridesJson = JsonSerializer.Serialize(canonical);
        return true;
    }

    private static List<string> SortTokens(IReadOnlyList<string> tokens)
    {
        if (tokens == null || tokens.Count == 0)
        {
            return new List<string>();
        }

        return tokens
            .Where(static token => !string.IsNullOrWhiteSpace(token))
            .Select(static token => token.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static token => token, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string ComputeSignature(string overridesJson, string generatedAtUtc)
    {
        var payload = $"{Schema}\n{generatedAtUtc}\n{overridesJson}";
        var bytes = Encoding.UTF8.GetBytes(payload);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }

    private sealed class PackageModel
    {
        public string Schema { get; set; } = string.Empty;

        public string GeneratedAtUtc { get; set; } = string.Empty;

        public string OverridesJson { get; set; } = string.Empty;

        public string Signature { get; set; } = string.Empty;
    }

    private sealed class CanonicalOverridesModel
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
