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
            TryLog(
                log,
                $"[SettingsMigration] migrated={migrated}; source={iniPath}; target={jsonPath}");
            return migrated;
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            TryLog(log, $"[SettingsMigration] bootstrap migration failed: {ex.GetType().Name} - {ex.Message}");
            return false;
        }
    }

    private static void TryLog(Action<string>? log, string message)
    {
        if (log == null)
        {
            return;
        }

        try
        {
            log(message);
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            System.Diagnostics.Debug.WriteLine($"SettingsDocumentBootstrapMigrationExecutor log callback failed: {ex.Message}");
        }
    }
}
