using System;

namespace ClassroomToolkit.App.Startup;

/// <summary>
/// Process-level switches for optional storage backends, read once during composition.
/// </summary>
internal static class StorageBackendFeatureFlags
{
    internal static bool PreferSqlite { get; } = ReadFlag("CTOOLKIT_USE_SQLITE_BUSINESS_STORE");

    internal static bool ExperimentalSqliteEnabled { get; } = ReadFlag("CTOOLKIT_ENABLE_EXPERIMENTAL_SQLITE_BACKEND");

    private static bool ReadFlag(string key)
    {
        var raw = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        raw = raw.Trim();
        return string.Equals(raw, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "on", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "yes", StringComparison.OrdinalIgnoreCase);
    }
}
