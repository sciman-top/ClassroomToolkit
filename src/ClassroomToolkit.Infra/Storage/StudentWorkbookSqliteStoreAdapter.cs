using System;
using System.Collections.Generic;
using System.Globalization;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using ClassroomToolkit.Domain.Models;
using ClassroomToolkit.Domain.Serialization;
using Microsoft.Data.Sqlite;

namespace ClassroomToolkit.Infra.Storage;

public sealed class StudentWorkbookSqliteStoreAdapter
{
    private const int SingletonRowId = 1;
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IStudentWorkbookStoreBridge _bridge;
    private readonly Func<string, string> _dbPathResolver;
    private readonly record struct RollStateSnapshot(string? Json, long? Revision, DateTime? UpdatedAtUtc);

    public StudentWorkbookSqliteStoreAdapter()
        : this(new StudentWorkbookStoreBridge(), ResolveDbPath)
    {
    }

    public StudentWorkbookSqliteStoreAdapter(IStudentWorkbookStoreBridge bridge)
        : this(bridge, ResolveDbPath)
    {
    }

    public StudentWorkbookSqliteStoreAdapter(IStudentWorkbookStoreBridge bridge, Func<string, string> dbPathResolver)
    {
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        _dbPathResolver = dbPathResolver ?? throw new ArgumentNullException(nameof(dbPathResolver));
    }

    public StudentWorkbookLoadResult LoadOrCreate(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var dbPath = ResolveDbPathSafe(path);
        StudentWorkbookLoadResult result;
        try
        {
            result = _bridge.LoadOrCreate(path);
        }
        catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine($"[StudentWorkbookSqlite] bridge load failed: {ex.GetType().Name} - {ex.Message}");
            if (TryReadWorkbookSnapshotPackage(dbPath, out var workbookFromSnapshot, out var rollStateFromSnapshot))
            {
                return new StudentWorkbookLoadResult(
                    workbookFromSnapshot,
                    CreatedTemplate: false,
                    RollStateJson: rollStateFromSnapshot);
            }

            throw;
        }

        var sqliteSnapshot = TryReadRollStateSnapshot(dbPath);
        RollStateSerializer.TryReadWorkbookMetadata(
            result.RollStateJson,
            out var authorityRevision,
            out var authorityMetadataUpdatedAtUtc);
        var authorityUpdatedAtUtc = authorityMetadataUpdatedAtUtc ?? TryReadAuthorityUpdatedAtUtc(path);
        var effectiveRollState = RollStateVersionArbitrationPolicy.Resolve(
            authorityStateJson: result.RollStateJson,
            authorityRevision: authorityRevision,
            authorityUpdatedAtUtc: authorityUpdatedAtUtc,
            cacheStateJson: sqliteSnapshot.Json,
            cacheRevision: sqliteSnapshot.Revision,
            cacheUpdatedAtUtc: sqliteSnapshot.UpdatedAtUtc,
            log: message => Debug.WriteLine(message),
            source: "StudentWorkbookSqlite");

        TryWriteSnapshotPackage(dbPath, result.Workbook, effectiveRollState);

        return result with { RollStateJson = effectiveRollState };
    }

    public void Save(StudentWorkbook workbook, string path, string? rollStateJson)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        _bridge.Save(workbook, path, rollStateJson);
        var dbPath = ResolveDbPathSafe(path);
        TryWriteSnapshotPackage(dbPath, workbook, rollStateJson);
    }

    private string ResolveDbPathSafe(string workbookPath)
    {
        try
        {
            var resolved = _dbPathResolver(workbookPath);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                return resolved;
            }

            Debug.WriteLine("[StudentWorkbookSqlite] resolver returned empty path; fallback to default path policy.");
        }
        catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine($"[StudentWorkbookSqlite] resolver failed: {ex.GetType().Name} - {ex.Message}");
        }

        return ResolveDbPath(workbookPath);
    }

    private static string ResolveDbPath(string workbookPath)
    {
        const string fallbackFileName = "students";
        try
        {
            var fullWorkbookPath = Path.GetFullPath(workbookPath);
            var directory = Path.GetDirectoryName(fullWorkbookPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                directory = AppContext.BaseDirectory;
            }

            var fileName = Path.GetFileNameWithoutExtension(fullWorkbookPath);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = fallbackFileName;
            }

            return Path.Combine(directory, $"{fileName}.studentworkbook.sqlite3");
        }
        catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
        {
            return Path.Combine(AppContext.BaseDirectory, $"{fallbackFileName}.studentworkbook.sqlite3");
        }
    }

    private static RollStateSnapshot TryReadRollStateSnapshot(string dbPath)
    {
        try
        {
            if (!File.Exists(dbPath))
            {
                return default;
            }

            using var connection = SqliteStorageUtilities.CreateOpenConnection(dbPath);
            EnsureSchema(connection);

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT roll_state_json, revision, updated_at_utc FROM student_workbook_state WHERE id = $id LIMIT 1;";
            command.Parameters.AddWithValue("$id", SingletonRowId);
            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return default;
            }

            var value = reader.IsDBNull(0) ? null : reader.GetString(0);
            long? revision = reader.IsDBNull(1) ? null : reader.GetInt64(1);
            var updatedAtRaw = reader.IsDBNull(2) ? null : reader.GetString(2);
            if (string.IsNullOrWhiteSpace(value))
            {
                return default;
            }

            return new RollStateSnapshot(value, revision, SqliteStorageUtilities.TryParseUtcTimestamp(updatedAtRaw));
        }
        catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine($"[StudentWorkbookSqlite] read failed: {ex.GetType().Name} - {ex.Message}");
            return default;
        }
    }

    private static bool TryReadWorkbookSnapshotPackage(
        string dbPath,
        out StudentWorkbook workbook,
        out string? rollStateJson)
    {
        workbook = default!;
        rollStateJson = null;

        try
        {
            if (!File.Exists(dbPath))
            {
                return false;
            }

            using var connection = SqliteStorageUtilities.CreateOpenConnection(dbPath);
            EnsureSchema(connection);

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT workbook_json FROM student_workbook_snapshot WHERE id = $id LIMIT 1;";
            command.Parameters.AddWithValue("$id", SingletonRowId);
            var scalar = command.ExecuteScalar();
            if (scalar is not string workbookJson || string.IsNullOrWhiteSpace(workbookJson))
            {
                return false;
            }

            var snapshot = JsonSerializer.Deserialize<WorkbookSnapshot>(workbookJson, SnapshotJsonOptions);
            if (snapshot == null)
            {
                return false;
            }

            workbook = ToWorkbook(snapshot);
            rollStateJson = TryReadRollStateSnapshot(dbPath).Json;
            return true;
        }
        catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine($"[StudentWorkbookSqlite] snapshot read failed: {ex.GetType().Name} - {ex.Message}");
            return false;
        }
    }

    private static void TryWriteSnapshotPackage(string dbPath, StudentWorkbook workbook, string? rollStateJson)
    {
        try
        {
            using var connection = SqliteStorageUtilities.CreateOpenConnection(dbPath);
            EnsureSchema(connection);
            RollStateSerializer.TryReadWorkbookMetadata(
                rollStateJson,
                out var rollStateRevision,
                out var rollStateUpdatedAtUtc);
            var effectiveRevision = rollStateRevision ?? DateTime.UtcNow.Ticks;
            var effectiveUpdatedAtUtc = (rollStateUpdatedAtUtc ?? DateTime.UtcNow).ToUniversalTime().ToString("O");

            using var transaction = connection.BeginTransaction();
            var snapshotJson = JsonSerializer.Serialize(FromWorkbook(workbook), SnapshotJsonOptions);
            using (var upsertWorkbook = connection.CreateCommand())
            {
                upsertWorkbook.Transaction = transaction;
                upsertWorkbook.CommandText =
                    """
                    INSERT INTO student_workbook_snapshot(id, workbook_json, updated_at_utc)
                    VALUES($id, $workbook, $updated)
                    ON CONFLICT(id) DO UPDATE SET
                        workbook_json = excluded.workbook_json,
                        updated_at_utc = excluded.updated_at_utc;
                    """;
                upsertWorkbook.Parameters.AddWithValue("$id", SingletonRowId);
                upsertWorkbook.Parameters.AddWithValue("$workbook", snapshotJson);
                upsertWorkbook.Parameters.AddWithValue("$updated", DateTime.UtcNow.ToString("O"));
                upsertWorkbook.ExecuteNonQuery();
            }

            if (string.IsNullOrWhiteSpace(rollStateJson))
            {
                using var deleteCommand = connection.CreateCommand();
                deleteCommand.Transaction = transaction;
                deleteCommand.CommandText = "DELETE FROM student_workbook_state WHERE id = $id;";
                deleteCommand.Parameters.AddWithValue("$id", SingletonRowId);
                deleteCommand.ExecuteNonQuery();
            }
            else
            {
                using var upsertState = connection.CreateCommand();
                upsertState.Transaction = transaction;
                upsertState.CommandText =
                    """
                    INSERT INTO student_workbook_state(id, roll_state_json, revision, updated_at_utc)
                    VALUES($id, $json, $revision, $updated)
                    ON CONFLICT(id) DO UPDATE SET
                        roll_state_json = excluded.roll_state_json,
                        revision = excluded.revision,
                        updated_at_utc = excluded.updated_at_utc;
                    """;
                upsertState.Parameters.AddWithValue("$id", SingletonRowId);
                upsertState.Parameters.AddWithValue("$json", rollStateJson);
                upsertState.Parameters.AddWithValue("$revision", effectiveRevision);
                upsertState.Parameters.AddWithValue("$updated", effectiveUpdatedAtUtc);
                upsertState.ExecuteNonQuery();
            }

            transaction.Commit();
        }
        catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine($"[StudentWorkbookSqlite] snapshot write failed: {ex.GetType().Name} - {ex.Message}");
        }
    }

    private static void EnsureSchema(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS student_workbook_state
            (
                id INTEGER PRIMARY KEY CHECK (id = 1),
                roll_state_json TEXT NULL,
                revision INTEGER NOT NULL DEFAULT 0,
                updated_at_utc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS student_workbook_snapshot
            (
                id INTEGER PRIMARY KEY CHECK (id = 1),
                workbook_json TEXT NOT NULL,
                updated_at_utc TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
        SqliteStorageUtilities.EnsureColumnExists(connection, "student_workbook_state", "revision", "INTEGER NOT NULL DEFAULT 0");
    }

    private static WorkbookSnapshot FromWorkbook(StudentWorkbook workbook)
    {
        var classes = workbook.Classes.Values
            .Select(roster => new ClassRosterSnapshot(
                roster.ClassName,
                roster.ColumnOrder.ToList(),
                roster.Students.Select(student => new StudentRecordSnapshot(
                    student.StudentId,
                    student.Name,
                    student.ClassName,
                    student.GroupName,
                    student.RowId,
                    student.RowKey,
                    new Dictionary<string, string>(student.ExtraFields, StringComparer.OrdinalIgnoreCase))).ToList()))
            .ToList();

        return new WorkbookSnapshot(workbook.ActiveClass, classes);
    }

    private static StudentWorkbook ToWorkbook(WorkbookSnapshot snapshot)
    {
        var classes = new Dictionary<string, ClassRoster>(StringComparer.OrdinalIgnoreCase);
        foreach (var classSnapshot in snapshot.Classes)
        {
            if (string.IsNullOrWhiteSpace(classSnapshot.ClassName))
            {
                continue;
            }

            var students = classSnapshot.Students.Select(student => new StudentRecord(
                    student.StudentId ?? string.Empty,
                    student.Name ?? string.Empty,
                    string.IsNullOrWhiteSpace(student.ClassName) ? classSnapshot.ClassName : student.ClassName,
                    student.GroupName ?? string.Empty,
                    string.IsNullOrWhiteSpace(student.RowId) ? Guid.NewGuid().ToString("N") : student.RowId,
                    student.RowKey ?? string.Empty,
                    student.ExtraFields ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)))
                .ToList();

            classes[classSnapshot.ClassName] = new ClassRoster(
                classSnapshot.ClassName,
                students,
                classSnapshot.ColumnOrder ?? ClassRoster.DefaultColumns.ToList());
        }

        return new StudentWorkbook(classes, snapshot.ActiveClass);
    }

    private sealed record WorkbookSnapshot(string ActiveClass, List<ClassRosterSnapshot> Classes);

    private sealed record ClassRosterSnapshot(
        string ClassName,
        List<string>? ColumnOrder,
        List<StudentRecordSnapshot> Students);

    private sealed record StudentRecordSnapshot(
        string StudentId,
        string Name,
        string ClassName,
        string GroupName,
        string RowId,
        string RowKey,
        Dictionary<string, string>? ExtraFields);

    private static DateTime? TryReadAuthorityUpdatedAtUtc(string workbookPath)
    {
        try
        {
            var fullWorkbookPath = Path.GetFullPath(workbookPath);
            if (!File.Exists(fullWorkbookPath))
            {
                return null;
            }

            return File.GetLastWriteTimeUtc(fullWorkbookPath);
        }
        catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine($"[StudentWorkbookSqlite] authority timestamp read failed: {ex.GetType().Name} - {ex.Message}");
            return null;
        }
    }

}
