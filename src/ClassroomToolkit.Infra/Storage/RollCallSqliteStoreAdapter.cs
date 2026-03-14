using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using ClassroomToolkit.Application.Abstractions;
using ClassroomToolkit.Domain.Models;
using Microsoft.Data.Sqlite;

namespace ClassroomToolkit.Infra.Storage;

public sealed class RollCallSqliteStoreAdapter : IRollCallWorkbookStore
{
    private const int SingletonRowId = 1;
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IRollCallWorkbookStore _bridge;
    private readonly Func<string, string> _dbPathResolver;

    public RollCallSqliteStoreAdapter()
        : this(new RollCallWorkbookStoreAdapter(), ResolveDbPath)
    {
    }

    public RollCallSqliteStoreAdapter(IRollCallWorkbookStore bridge)
        : this(bridge, ResolveDbPath)
    {
    }

    public RollCallSqliteStoreAdapter(IRollCallWorkbookStore bridge, Func<string, string> dbPathResolver)
    {
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        _dbPathResolver = dbPathResolver ?? throw new ArgumentNullException(nameof(dbPathResolver));
    }

    public RollCallWorkbookStoreLoadData LoadOrCreate(string path)
    {
        var dbPath = _dbPathResolver(path);
        RollCallWorkbookStoreLoadData result;
        try
        {
            result = _bridge.LoadOrCreate(path);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RollCallSqlite] bridge load failed: {ex.GetType().Name} - {ex.Message}");
            if (TryReadWorkbookSnapshotPackage(dbPath, out var workbookFromSnapshot, out var rollStateFromSnapshot))
            {
                return new RollCallWorkbookStoreLoadData(
                    workbookFromSnapshot,
                    CreatedTemplate: false,
                    RollStateJson: rollStateFromSnapshot);
            }

            throw;
        }

        var sqliteState = TryReadRollState(dbPath);
        var effectiveRollState = !string.IsNullOrWhiteSpace(sqliteState)
            ? sqliteState
            : result.RollStateJson;

        TryWriteSnapshotPackage(dbPath, result.Workbook, effectiveRollState);

        return result with { RollStateJson = effectiveRollState };
    }

    public void Save(StudentWorkbook workbook, string path, string? rollStateJson)
    {
        _bridge.Save(workbook, path, rollStateJson);
        var dbPath = _dbPathResolver(path);
        TryWriteSnapshotPackage(dbPath, workbook, rollStateJson);
    }

    private static string ResolveDbPath(string workbookPath)
    {
        var fullWorkbookPath = Path.GetFullPath(workbookPath);
        var directory = Path.GetDirectoryName(fullWorkbookPath) ?? AppContext.BaseDirectory;
        var fileName = Path.GetFileNameWithoutExtension(fullWorkbookPath);
        return Path.Combine(directory, $"{fileName}.rollcall.sqlite3");
    }

    private static string? TryReadRollState(string dbPath)
    {
        try
        {
            if (!File.Exists(dbPath))
            {
                return null;
            }

            using var connection = CreateOpenConnection(dbPath);
            EnsureSchema(connection);

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT roll_state_json FROM roll_call_state WHERE id = $id LIMIT 1;";
            command.Parameters.AddWithValue("$id", SingletonRowId);
            var scalar = command.ExecuteScalar();
            return scalar is string value && !string.IsNullOrWhiteSpace(value)
                ? value
                : null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RollCallSqlite] read failed: {ex.GetType().Name} - {ex.Message}");
            return null;
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

            using var connection = CreateOpenConnection(dbPath);
            EnsureSchema(connection);

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT workbook_json FROM roll_call_workbook_snapshot WHERE id = $id LIMIT 1;";
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
            rollStateJson = TryReadRollState(dbPath);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RollCallSqlite] snapshot read failed: {ex.GetType().Name} - {ex.Message}");
            return false;
        }
    }

    private static void TryWriteSnapshotPackage(string dbPath, StudentWorkbook workbook, string? rollStateJson)
    {
        try
        {
            using var connection = CreateOpenConnection(dbPath);
            EnsureSchema(connection);

            using var transaction = connection.BeginTransaction();
            var snapshotJson = JsonSerializer.Serialize(FromWorkbook(workbook), SnapshotJsonOptions);
            using (var upsertWorkbook = connection.CreateCommand())
            {
                upsertWorkbook.Transaction = transaction;
                upsertWorkbook.CommandText =
                    """
                    INSERT INTO roll_call_workbook_snapshot(id, workbook_json, updated_at_utc)
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
                deleteCommand.CommandText = "DELETE FROM roll_call_state WHERE id = $id;";
                deleteCommand.Parameters.AddWithValue("$id", SingletonRowId);
                deleteCommand.ExecuteNonQuery();
            }
            else
            {
                using var upsertState = connection.CreateCommand();
                upsertState.Transaction = transaction;
                upsertState.CommandText =
                    """
                    INSERT INTO roll_call_state(id, roll_state_json, updated_at_utc)
                    VALUES($id, $json, $updated)
                    ON CONFLICT(id) DO UPDATE SET
                        roll_state_json = excluded.roll_state_json,
                        updated_at_utc = excluded.updated_at_utc;
                    """;
                upsertState.Parameters.AddWithValue("$id", SingletonRowId);
                upsertState.Parameters.AddWithValue("$json", rollStateJson);
                upsertState.Parameters.AddWithValue("$updated", DateTime.UtcNow.ToString("O"));
                upsertState.ExecuteNonQuery();
            }

            transaction.Commit();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RollCallSqlite] snapshot write failed: {ex.GetType().Name} - {ex.Message}");
        }
    }

    private static SqliteConnection CreateOpenConnection(string dbPath)
    {
        var directory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();
        return connection;
    }

    private static void EnsureSchema(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS roll_call_state
            (
                id INTEGER PRIMARY KEY CHECK (id = 1),
                roll_state_json TEXT NULL,
                updated_at_utc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS roll_call_workbook_snapshot
            (
                id INTEGER PRIMARY KEY CHECK (id = 1),
                workbook_json TEXT NOT NULL,
                updated_at_utc TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
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
                    student.ClassName ?? classSnapshot.ClassName,
                    student.GroupName ?? string.Empty,
                    student.RowId ?? Guid.NewGuid().ToString("N"),
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
}
