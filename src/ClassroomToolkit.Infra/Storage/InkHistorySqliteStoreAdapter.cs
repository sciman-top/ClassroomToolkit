using System;
using System.Diagnostics;
using System.IO;
using ClassroomToolkit.Application.Abstractions;
using Microsoft.Data.Sqlite;

namespace ClassroomToolkit.Infra.Storage;

public sealed class InkHistorySqliteStoreAdapter
{
    private readonly IInkHistoryStoreBridge _bridge;
    private readonly Func<string, string> _dbPathResolver;

    public InkHistorySqliteStoreAdapter(IInkHistoryStoreBridge bridge)
        : this(bridge, ResolveDbPath)
    {
    }

    public InkHistorySqliteStoreAdapter(IInkHistoryStoreBridge bridge, Func<string, string> dbPathResolver)
    {
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        _dbPathResolver = dbPathResolver ?? throw new ArgumentNullException(nameof(dbPathResolver));
    }

    public InkHistoryLoadResult LoadOrCreate(string sourcePath, int pageIndex, bool writeSnapshot = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);

        var dbPath = ResolveDbPathSafe(sourcePath);
        InkHistoryLoadResult result;
        try
        {
            result = _bridge.LoadOrCreate(sourcePath, pageIndex);
        }
        catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine($"[InkHistorySqlite] bridge load failed: {ex.GetType().Name} - {ex.Message}");
            var fallbackJson = TryReadSnapshot(dbPath, sourcePath, pageIndex);
            if (fallbackJson != null)
            {
                return new InkHistoryLoadResult(sourcePath, pageIndex, fallbackJson, CreatedTemplate: false);
            }

            throw;
        }

        var sqliteJson = TryReadSnapshot(dbPath, sourcePath, pageIndex);
        var effectiveJson = sqliteJson ?? result.StrokesJson;
        if (writeSnapshot)
        {
            TryWriteSnapshot(dbPath, sourcePath, pageIndex, effectiveJson);
        }
        return result with { StrokesJson = effectiveJson };
    }

    public void Save(string sourcePath, int pageIndex, string? strokesJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);

        _bridge.Save(sourcePath, pageIndex, strokesJson);
        var dbPath = ResolveDbPathSafe(sourcePath);
        TryWriteSnapshot(dbPath, sourcePath, pageIndex, strokesJson);
    }

    private string ResolveDbPathSafe(string sourcePath)
    {
        try
        {
            var resolved = _dbPathResolver(sourcePath);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                return resolved;
            }

            Debug.WriteLine("[InkHistorySqlite] resolver returned empty path; fallback to default path policy.");
        }
        catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine($"[InkHistorySqlite] resolver failed: {ex.GetType().Name} - {ex.Message}");
        }

        return ResolveDbPath(sourcePath);
    }

    private static string ResolveDbPath(string sourcePath)
    {
        const string fallbackFileName = "inkhistory";
        try
        {
            var fullSourcePath = Path.GetFullPath(sourcePath);
            var directory = Path.GetDirectoryName(fullSourcePath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                directory = AppContext.BaseDirectory;
            }

            var fileName = Path.GetFileNameWithoutExtension(fullSourcePath);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = fallbackFileName;
            }

            return Path.Combine(directory, $"{fileName}.inkhistory.sqlite3");
        }
        catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
        {
            return Path.Combine(AppContext.BaseDirectory, $"{fallbackFileName}.inkhistory.sqlite3");
        }
    }

    private static string? TryReadSnapshot(string dbPath, string sourcePath, int pageIndex)
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
            command.CommandText =
                """
                SELECT strokes_json
                FROM ink_history_snapshot
                WHERE source_path = $sourcePath AND page_index = $pageIndex
                LIMIT 1;
                """;
            command.Parameters.AddWithValue("$sourcePath", sourcePath);
            command.Parameters.AddWithValue("$pageIndex", pageIndex);
            var scalar = command.ExecuteScalar();
            return scalar as string;
        }
        catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine($"[InkHistorySqlite] read failed: {ex.GetType().Name} - {ex.Message}");
            return null;
        }
    }

    private static void TryWriteSnapshot(string dbPath, string sourcePath, int pageIndex, string? strokesJson)
    {
        try
        {
            using var connection = CreateOpenConnection(dbPath);
            EnsureSchema(connection);

            using var command = connection.CreateCommand();
            if (string.IsNullOrWhiteSpace(strokesJson))
            {
                command.CommandText =
                    """
                    DELETE FROM ink_history_snapshot
                    WHERE source_path = $sourcePath AND page_index = $pageIndex;
                    """;
                command.Parameters.AddWithValue("$sourcePath", sourcePath);
                command.Parameters.AddWithValue("$pageIndex", pageIndex);
                command.ExecuteNonQuery();
                return;
            }

            command.CommandText =
                """
                INSERT INTO ink_history_snapshot(source_path, page_index, strokes_json, updated_at_utc)
                VALUES($sourcePath, $pageIndex, $strokes, $updatedAtUtc)
                ON CONFLICT(source_path, page_index) DO UPDATE SET
                    strokes_json = excluded.strokes_json,
                    updated_at_utc = excluded.updated_at_utc;
                """;
            command.Parameters.AddWithValue("$sourcePath", sourcePath);
            command.Parameters.AddWithValue("$pageIndex", pageIndex);
            command.Parameters.AddWithValue("$strokes", strokesJson);
            command.Parameters.AddWithValue("$updatedAtUtc", DateTime.UtcNow.ToString("O"));
            command.ExecuteNonQuery();
        }
        catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine($"[InkHistorySqlite] write failed: {ex.GetType().Name} - {ex.Message}");
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
            CREATE TABLE IF NOT EXISTS ink_history_snapshot
            (
                source_path TEXT NOT NULL,
                page_index INTEGER NOT NULL,
                strokes_json TEXT NOT NULL,
                updated_at_utc TEXT NOT NULL,
                PRIMARY KEY(source_path, page_index)
            );
            """;
        command.ExecuteNonQuery();
    }
}
