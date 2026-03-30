using ClassroomToolkit.App.Settings;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class SettingsDocumentBootstrapMigrationPolicyTests
{
    [Fact]
    public void Resolve_ShouldSkip_WhenFormatIsIni()
    {
        var decision = SettingsDocumentBootstrapMigrationPolicy.Resolve(
            settingsDocumentFormat: SettingsDocumentFormat.Ini,
            settingsDocumentExists: false,
            settingsIniExists: true);

        decision.ShouldMigrate.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldSkip_WhenJsonAlreadyExists()
    {
        var decision = SettingsDocumentBootstrapMigrationPolicy.Resolve(
            settingsDocumentFormat: SettingsDocumentFormat.Json,
            settingsDocumentExists: true,
            settingsIniExists: true);

        decision.ShouldMigrate.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldSkip_WhenIniMissing()
    {
        var decision = SettingsDocumentBootstrapMigrationPolicy.Resolve(
            settingsDocumentFormat: SettingsDocumentFormat.Json,
            settingsDocumentExists: false,
            settingsIniExists: false);

        decision.ShouldMigrate.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldMigrate_WhenJsonMissingAndIniExists()
    {
        var decision = SettingsDocumentBootstrapMigrationPolicy.Resolve(
            settingsDocumentFormat: SettingsDocumentFormat.Json,
            settingsDocumentExists: false,
            settingsIniExists: true);

        decision.ShouldMigrate.Should().BeTrue();
    }
}
