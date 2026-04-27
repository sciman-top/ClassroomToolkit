namespace ClassroomToolkit.App.Settings;

internal readonly record struct SettingsDocumentBootstrapMigrationDecision(bool ShouldMigrate);

internal static class SettingsDocumentBootstrapMigrationPolicy
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
