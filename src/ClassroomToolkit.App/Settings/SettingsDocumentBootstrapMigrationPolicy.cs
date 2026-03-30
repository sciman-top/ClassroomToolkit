namespace ClassroomToolkit.App.Settings;

public readonly record struct SettingsDocumentBootstrapMigrationDecision(bool ShouldMigrate);

public static class SettingsDocumentBootstrapMigrationPolicy
{
    public static SettingsDocumentBootstrapMigrationDecision Resolve(
        SettingsDocumentFormat settingsDocumentFormat,
        bool settingsDocumentExists,
        bool settingsIniExists)
    {
        var shouldMigrate = settingsDocumentFormat == SettingsDocumentFormat.Json
                            && !settingsDocumentExists
                            && settingsIniExists;
        return new SettingsDocumentBootstrapMigrationDecision(shouldMigrate);
    }
}
