using ClassroomToolkit.Domain.Models;
using System;

namespace ClassroomToolkit.Infra.Storage;

public enum BusinessStorageBackend
{
    ExcelWorkbook = 0,
    Sqlite = 1
}

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

public interface IStudentWorkbookStoreBridge
{
    StudentWorkbookLoadResult LoadOrCreate(string path);
    void Save(StudentWorkbook workbook, string path, string? rollStateJson);
}

public sealed class StudentWorkbookStoreBridge : IStudentWorkbookStoreBridge
{
    private readonly StudentWorkbookStore _store = new();

    public StudentWorkbookLoadResult LoadOrCreate(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return _store.LoadOrCreate(path);
    }

    public void Save(StudentWorkbook workbook, string path, string? rollStateJson)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        _store.Save(workbook, path, rollStateJson);
    }
}
