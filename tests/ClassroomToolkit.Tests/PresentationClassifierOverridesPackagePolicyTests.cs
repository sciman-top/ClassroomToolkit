using System.Text.Json;
using ClassroomToolkit.Interop.Presentation;
using ClassroomToolkit.Services.Presentation;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PresentationClassifierOverridesPackagePolicyTests
{
    [Fact]
    public void ExportThenImport_ShouldRoundTripCanonicalOverrides()
    {
        var sourceOverridesJson =
            """
            {
              "AdditionalWpsClassTokens": ["b", "a", "A"],
              "AdditionalOfficeProcessTokens": ["ppt_gov.exe"]
            }
            """;

        var exported = PresentationClassifierOverridesPackagePolicy.TryExport(
            sourceOverridesJson,
            out var packageJson,
            out var exportError);
        var imported = PresentationClassifierOverridesPackagePolicy.TryImport(
            packageJson,
            out var importedOverridesJson,
            out var detail,
            out var importError);

        exported.Should().BeTrue();
        exportError.Should().BeEmpty();
        imported.Should().BeTrue();
        importError.Should().BeEmpty();
        detail.Should().Contain("classToken=");
        detail.Should().Contain("processToken=");

        PresentationClassifierOverridesParser.TryParse(importedOverridesJson, out var overrides, out var _)
            .Should()
            .BeTrue();
        overrides.AdditionalWpsClassTokens.Should().Equal("a", "b");
        overrides.AdditionalOfficeProcessTokens.Should().Contain("ppt_gov.exe");
    }

    [Fact]
    public void Import_ShouldFail_WhenSignatureTampered()
    {
        PresentationClassifierOverridesPackagePolicy.TryExport(
                """{ "AdditionalWpsClassTokens": ["w"] }""",
                out var packageJson,
                out var _)
            .Should()
            .BeTrue();

        using var doc = JsonDocument.Parse(packageJson);
        var root = doc.RootElement;
        var tampered = JsonSerializer.Serialize(
            new
            {
                Schema = root.GetProperty("Schema").GetString(),
                GeneratedAtUtc = root.GetProperty("GeneratedAtUtc").GetString(),
                OverridesJson = root.GetProperty("OverridesJson").GetString(),
                Signature = "tampered"
            });

        var imported = PresentationClassifierOverridesPackagePolicy.TryImport(
            tampered,
            out var _,
            out var _,
            out var error);

        imported.Should().BeFalse();
        error.Should().Be("package-signature-mismatch");
    }

    [Fact]
    public void Import_ShouldFail_WhenSchemaUnsupported()
    {
        PresentationClassifierOverridesPackagePolicy.TryExport(
                """{ "AdditionalWpsClassTokens": ["w"] }""",
                out var packageJson,
                out var _)
            .Should()
            .BeTrue();

        using var doc = JsonDocument.Parse(packageJson);
        var root = doc.RootElement;
        var tampered = JsonSerializer.Serialize(
            new
            {
                Schema = "unknown-schema",
                GeneratedAtUtc = root.GetProperty("GeneratedAtUtc").GetString(),
                OverridesJson = root.GetProperty("OverridesJson").GetString(),
                Signature = root.GetProperty("Signature").GetString()
            });

        var imported = PresentationClassifierOverridesPackagePolicy.TryImport(
            tampered,
            out var _,
            out var _,
            out var error);

        imported.Should().BeFalse();
        error.Should().Be("package-schema-unsupported");
    }

    [Fact]
    public void Export_ShouldFail_WhenOverridesInvalid()
    {
        var exported = PresentationClassifierOverridesPackagePolicy.TryExport(
            "{not-json",
            out var _,
            out var error);

        exported.Should().BeFalse();
        error.Should().StartWith("overrides-parse-failed:");
    }
}
