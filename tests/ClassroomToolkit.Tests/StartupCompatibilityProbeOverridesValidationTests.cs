using ClassroomToolkit.Services.Compatibility;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class StartupCompatibilityProbeOverridesValidationTests
{
    [Fact]
    public void TryValidateClassifierOverrides_ShouldReturnTrue_WhenEmpty()
    {
        var success = StartupCompatibilityProbe.TryValidateClassifierOverrides(null, out var error);

        success.Should().BeTrue();
        error.Should().BeEmpty();
    }

    [Fact]
    public void TryValidateClassifierOverrides_ShouldReturnFalse_WhenJsonInvalid()
    {
        var success = StartupCompatibilityProbe.TryValidateClassifierOverrides("{not-json", out var error);

        success.Should().BeFalse();
        error.Should().Contain("parse");
    }

    [Fact]
    public void Collect_ShouldIncludeOverridesWarning_WhenOverridesInvalid()
    {
        var settingsPath = TestPathHelper.CreateFilePath("startup_compat", ".json");

        try
        {
            var report = StartupCompatibilityProbe.Collect(settingsPath, "{not-json");

            report.Issues.Should().Contain(issue => issue.Code == "presentation-classifier-overrides-invalid");
            report.Issues.Should().Contain(issue => !issue.IsBlocking);
        }
        finally
        {
            if (File.Exists(settingsPath))
            {
                File.Delete(settingsPath);
            }
        }
    }
}
