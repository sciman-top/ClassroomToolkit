using ClassroomToolkit.App;

namespace ClassroomToolkit.App.Settings;

public static class SettingsDocumentBootstrapMigrationExecutor
{
    public static bool TryMigrate(
        SettingsDocumentBootstrapMigrationDecision decision,
        string iniPath,
        string jsonPath,
        Func<string, string, bool, bool> migrate,
        Action<string>? log)
    {
        ArgumentNullException.ThrowIfNull(migrate);

        if (!decision.ShouldMigrate)
        {
            return false;
        }

        try
        {
            var migrated = migrate(iniPath, jsonPath, false);
            log?.Invoke(
                $"[SettingsMigration] migrated={migrated}; source={iniPath}; target={jsonPath}");
            return migrated;
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            log?.Invoke($"[SettingsMigration] bootstrap migration failed: {ex.GetType().Name} - {ex.Message}");
            return false;
        }
    }
}
