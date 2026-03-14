namespace ClassroomToolkit.Infra.Storage;

public static class BusinessStorageBackendSelectionPolicy
{
    public static BusinessStorageBackend Resolve(bool preferSqlite, bool sqliteAvailable)
    {
        if (preferSqlite && sqliteAvailable)
        {
            return BusinessStorageBackend.Sqlite;
        }

        return BusinessStorageBackend.ExcelWorkbook;
    }
}
