using System;

namespace ClassroomToolkit.Infra.Storage;

public static class BusinessStorageBackendCapabilityPolicy
{
    public static bool IsSqliteAvailable(bool experimentalEnabled)
    {
        if (!experimentalEnabled)
        {
            return false;
        }

        // Probe runtime availability without hard dependency at compile-time.
        var sqliteType = Type.GetType("Microsoft.Data.Sqlite.SqliteConnection, Microsoft.Data.Sqlite", throwOnError: false);
        return sqliteType != null;
    }
}
