using System;
using ClassroomToolkit.Application.Abstractions;

namespace ClassroomToolkit.Infra.Storage;

public static class RollCallWorkbookStoreResolver
{
    public static IRollCallWorkbookStore Create(bool preferSqlite, bool experimentalSqliteEnabled)
        => Create(
            preferSqlite,
            experimentalSqliteEnabled,
            out _,
            BusinessStorageBackendCapabilityPolicy.IsSqliteAvailable);

    public static IRollCallWorkbookStore Create(
        bool preferSqlite,
        bool experimentalSqliteEnabled,
        out BusinessStorageBackend selectedBackend)
        => Create(
            preferSqlite,
            experimentalSqliteEnabled,
            out selectedBackend,
            BusinessStorageBackendCapabilityPolicy.IsSqliteAvailable);

    public static IRollCallWorkbookStore Create(
        bool preferSqlite,
        bool experimentalSqliteEnabled,
        out BusinessStorageBackend selectedBackend,
        Func<bool, bool> sqliteAvailabilityEvaluator)
    {
        ArgumentNullException.ThrowIfNull(sqliteAvailabilityEvaluator);

        var sqliteAvailable = ResolveSqliteAvailability(
            experimentalSqliteEnabled,
            sqliteAvailabilityEvaluator);
        var backend = BusinessStorageBackendSelectionPolicy.Resolve(
            preferSqlite: preferSqlite,
            sqliteAvailable: sqliteAvailable);
        selectedBackend = backend;

        return backend switch
        {
            BusinessStorageBackend.Sqlite => new StudentWorkbookSqliteRollCallStoreAdapter(),
            _ => new RollCallWorkbookStoreAdapter()
        };
    }

    internal static bool ResolveSqliteAvailability(
        bool experimentalSqliteEnabled,
        Func<bool, bool> sqliteAvailabilityEvaluator)
    {
        ArgumentNullException.ThrowIfNull(sqliteAvailabilityEvaluator);
        try
        {
            return sqliteAvailabilityEvaluator(experimentalSqliteEnabled);
        }
        catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
        {
            return false;
        }
    }
}
