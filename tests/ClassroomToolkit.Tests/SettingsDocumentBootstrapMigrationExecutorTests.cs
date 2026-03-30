using ClassroomToolkit.App.Settings;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class SettingsDocumentBootstrapMigrationExecutorTests
{
    [Fact]
    public void TryMigrate_ShouldSkip_WhenDecisionDisablesMigration()
    {
        var called = false;
        var result = SettingsDocumentBootstrapMigrationExecutor.TryMigrate(
            new SettingsDocumentBootstrapMigrationDecision(ShouldMigrate: false),
            "a.ini",
            "b.json",
            (_, _, _) =>
            {
                called = true;
                return false;
            },
            _ => { });

        result.Should().BeFalse();
        called.Should().BeFalse();
    }

    [Fact]
    public void TryMigrate_ShouldReturnTrue_WhenMigrationRuns()
    {
        var called = false;
        var result = SettingsDocumentBootstrapMigrationExecutor.TryMigrate(
            new SettingsDocumentBootstrapMigrationDecision(ShouldMigrate: true),
            "a.ini",
            "b.json",
            (iniPath, jsonPath, overwrite) =>
            {
                called = true;
                iniPath.Should().Be("a.ini");
                jsonPath.Should().Be("b.json");
                overwrite.Should().BeFalse();
                return true;
            },
            _ => { });

        result.Should().BeTrue();
        called.Should().BeTrue();
    }

    [Fact]
    public void TryMigrate_ShouldSwallowException_AndReturnFalse()
    {
        var logged = false;
        var result = SettingsDocumentBootstrapMigrationExecutor.TryMigrate(
            new SettingsDocumentBootstrapMigrationDecision(ShouldMigrate: true),
            "a.ini",
            "b.json",
            (_, _, _) => throw new InvalidOperationException("boom"),
            _ => logged = true);

        result.Should().BeFalse();
        logged.Should().BeTrue();
    }

    [Fact]
    public void TryMigrate_ShouldRethrowFatalException()
    {
        var act = () => SettingsDocumentBootstrapMigrationExecutor.TryMigrate(
            new SettingsDocumentBootstrapMigrationDecision(ShouldMigrate: true),
            "a.ini",
            "b.json",
            (_, _, _) => throw new BadImageFormatException("fatal"),
            _ => { });

        act.Should().Throw<BadImageFormatException>();
    }
}
