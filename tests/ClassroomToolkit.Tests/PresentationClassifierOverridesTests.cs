using ClassroomToolkit.Interop.Presentation;
using FluentAssertions;
using System.Linq;

namespace ClassroomToolkit.Tests;

public sealed class PresentationClassifierOverridesTests
{
    [Fact]
    public void TryParse_ShouldReturnFalse_WhenJsonInvalid()
    {
        var success = PresentationClassifierOverridesParser.TryParse(
            "{not-json",
            out var overrides,
            out var error);

        success.Should().BeFalse();
        overrides.Should().Be(PresentationClassifierOverrides.Empty);
        error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void TryParse_ShouldLoadAdditionalTokens_WhenJsonValid()
    {
        var json =
            """
            {
              "AdditionalWpsClassTokens": ["gov_wps_show_class"],
              "AdditionalOfficeProcessTokens": ["powerpoint_gov"],
              "ClassMatchWeight": 8
            }
            """;

        var success = PresentationClassifierOverridesParser.TryParse(json, out var overrides, out var _);

        success.Should().BeTrue();
        overrides.AdditionalWpsClassTokens.Should().Contain("gov_wps_show_class");
        overrides.AdditionalOfficeProcessTokens.Should().Contain("powerpoint_gov");
    }

    [Fact]
    public void TryParseScoringOptions_ShouldReadConfiguredWeights_WhenJsonValid()
    {
        var json =
            """
            {
              "ClassMatchWeight": 7,
              "ProcessMatchWeight": 5,
              "NoCaptionWeight": 2,
              "IsFullscreenWeight": 4,
              "FullscreenClassMatchBonus": 66,
              "RequireClassMatchOrFullscreen": false,
              "MinimumCandidateScore": 3
            }
            """;

        var success = PresentationClassifierOverridesParser.TryParseScoringOptions(json, out var options, out var _);

        success.Should().BeTrue();
        options.ClassMatchWeight.Should().Be(7);
        options.ProcessMatchWeight.Should().Be(5);
        options.NoCaptionWeight.Should().Be(2);
        options.IsFullscreenWeight.Should().Be(4);
        options.FullscreenClassMatchBonus.Should().Be(66);
        options.RequireClassMatchOrFullscreen.Should().BeFalse();
        options.MinimumCandidateScore.Should().Be(3);
    }

    [Fact]
    public void TryParseScoringOptions_ShouldClampNegativeValues_ToZero()
    {
        var json =
            """
            {
              "ClassMatchWeight": -1,
              "MinimumCandidateScore": -99
            }
            """;

        var success = PresentationClassifierOverridesParser.TryParseScoringOptions(json, out var options, out var _);

        success.Should().BeTrue();
        options.ClassMatchWeight.Should().Be(0);
        options.MinimumCandidateScore.Should().Be(0);
    }

    [Fact]
    public void Classifier_ShouldUseOfficeProcessOverrides_WhenProvided()
    {
        var overrides = new PresentationClassifierOverrides(
            AdditionalWpsClassTokens: Array.Empty<string>(),
            AdditionalOfficeClassTokens: Array.Empty<string>(),
            AdditionalSlideshowClassTokens: new[] { "custom_slideshow_class" },
            AdditionalWpsProcessTokens: Array.Empty<string>(),
            AdditionalOfficeProcessTokens: new[] { "powerpoint_gov" });
        var classifier = new PresentationClassifier(overrides);
        var info = new PresentationWindowInfo(
            10,
            "powerpoint_gov.exe",
            new[] { "custom_slideshow_class" });

        var type = classifier.Classify(info);

        type.Should().Be(PresentationType.Office);
    }

    [Fact]
    public void AutoLearn_ShouldMergeOfficeForegroundSignature_AndPreserveScoring()
    {
        var existingJson =
            """
            {
              "AdditionalOfficeProcessTokens": ["powerpnt"],
              "MinimumCandidateScore": 6
            }
            """;
        var info = new PresentationWindowInfo(
            42,
            "pptgov.exe",
            new[] { "GovPptShowClass", "screenClass" });

        var built = PresentationClassifierAutoLearnPolicy.TryBuildRequest(
            info,
            PresentationType.Office,
            out var request);
        var merged = PresentationClassifierAutoLearnPolicy.TryMergeOverridesJson(
            existingJson,
            request,
            out var mergedJson,
            out var error);

        built.Should().BeTrue();
        merged.Should().BeTrue();
        error.Should().BeEmpty();
        PresentationClassifierOverridesParser.TryParse(mergedJson, out var overrides, out var _).Should().BeTrue();
        PresentationClassifierOverridesParser.TryParseScoringOptions(mergedJson, out var scoring, out var _).Should().BeTrue();
        overrides.AdditionalOfficeClassTokens.Should().Contain("GovPptShowClass");
        overrides.AdditionalSlideshowClassTokens.Should().Contain("GovPptShowClass");
        overrides.AdditionalOfficeProcessTokens.Should().Contain("pptgov");
        scoring.MinimumCandidateScore.Should().Be(6);
    }

    [Fact]
    public void AutoLearn_ShouldReturnFalse_WhenExistingOverridesJsonInvalid()
    {
        var request = new PresentationClassifierLearnRequest(
            PresentationType.Wps,
            new[] { "GovWpsShowClass" },
            "wppgov");

        var success = PresentationClassifierAutoLearnPolicy.TryMergeOverridesJson(
            "{not-json",
            request,
            out var _,
            out var error);

        success.Should().BeFalse();
        error.Should().Contain("parse");
    }

    [Fact]
    public void AutoLearn_ShouldNotDuplicateTokens_WhenAlreadyExists()
    {
        var existingJson =
            """
            {
              "AdditionalWpsClassTokens": ["GovWpsClass"],
              "AdditionalSlideshowClassTokens": ["GovWpsClass"],
              "AdditionalWpsProcessTokens": ["wppgov"]
            }
            """;
        var request = new PresentationClassifierLearnRequest(
            PresentationType.Wps,
            new[] { "GovWpsClass" },
            "wppgov");

        var success = PresentationClassifierAutoLearnPolicy.TryMergeOverridesJson(
            existingJson,
            request,
            out var mergedJson,
            out var error);

        success.Should().BeTrue();
        error.Should().BeEmpty();
        mergedJson.Should().NotBeNullOrWhiteSpace();
        PresentationClassifierOverridesParser.TryParse(mergedJson, out var overrides, out var _).Should().BeTrue();
        overrides.AdditionalWpsClassTokens.Count(token => token == "GovWpsClass").Should().Be(1);
        overrides.AdditionalSlideshowClassTokens.Count(token => token == "GovWpsClass").Should().Be(1);
        overrides.AdditionalWpsProcessTokens.Count(token => token == "wppgov").Should().Be(1);
    }
}
