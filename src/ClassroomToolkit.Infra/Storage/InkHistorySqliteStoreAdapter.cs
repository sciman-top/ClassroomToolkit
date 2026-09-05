using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using ClassroomToolkit.Application.Abstractions;
using Microsoft.Data.Sqlite;

namespace ClassroomToolkit.Infra.Storage;

public sealed class InkHistorySqliteStoreAdapter
{
    private readonly IInkHistoryStoreBridge _bridge;
    private readonly Func<string, string> _dbPathResolver;

    private sealed record InkSnapshot(string? StrokesJson, DateTime? UpdatedAtUtc);

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
        ThrowIfInvalidPageIndex(pageIndex);

        var dbPath = ResolveDbPathSafe(sourcePath);
        InkHistoryLoadResult result;
        try
        {
            result = _bridge.LoadOrCreate(sourcePath, pageIndex);
        }
        catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine($"[InkHistorySqlite] bridge load failed: {ex.GetType().Name} - {ex.Message}");
            var fallback = TryReadSnapshot(dbPath, sourcePath, pageIndex);
            if (fallback != null && !string.IsNullOrWhiteSpace(fallback.StrokesJson))
            {
                return new InkHistoryLoadResult(
                    sourcePath,
                    pageIndex,
                    fallback.StrokesJson,
                    CreatedTemplate: false,
                    fallback.UpdatedAtUtc);
            }

            throw;
        }

        var sqliteSnapshot = TryReadSnapshot(dbPath, sourcePath, pageIndex);
        var effective = ResolveEffectiveSnapshot(result, sqliteSnapshot);
        if (writeSnapshot)
        {
            TryWriteSnapshot(dbPath, sourcePath, pageIndex, effective.StrokesJson, effective.UpdatedAtUtc);
        }
        return result with
        {
            StrokesJson = effective.StrokesJson,
            UpdatedAtUtc = effective.UpdatedAtUtc
        };
    }

    public void Save(string sourcePath, int pageIndex, string? strokesJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ThrowIfInvalidPageIndex(pageIndex);

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

    private static InkSnapshot? TryReadSnapshot(string dbPath, string sourcePath, int pageIndex)
    {
        try
        {
            if (!File.Exists(dbPath))
            {
                return null;
            }

            using var connection = SqliteStorageUtilities.CreateOpenConnection(dbPath);
            EnsureSchema(connection);

            using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT strokes_json, updated_at_utc
                FROM ink_history_snapshot
                WHERE source_path = $sourcePath AND page_index = $pageIndex
                LIMIT 1;
                """;
            command.Parameters.AddWithValue("$sourcePath", sourcePath);
            command.Parameters.AddWithValue("$pageIndex", pageIndex);
            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return null;
            }

            var strokesJson = reader.IsDBNull(0) ? null : reader.GetString(0);
            var updatedAtUtc = reader.IsDBNull(1)
                ? null
                : ParseUpdatedAtUtc(reader.GetString(1));
            return new InkSnapshot(strokesJson, updatedAtUtc);
        }
        catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine($"[InkHistorySqlite] read failed: {ex.GetType().Name} - {ex.Message}");
            return null;
        }
    }

    private static void TryWriteSnapshot(
        string dbPath,
        string sourcePath,
        int pageIndex,
        string? strokesJson,
        DateTime? updatedAtUtc = null)
    {
        try
        {
            using var connection = SqliteStorageUtilities.CreateOpenConnection(dbPath);
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
            command.Parameters.AddWithValue(
                "$updatedAtUtc",
                (updatedAtUtc ?? DateTime.UtcNow).ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            command.ExecuteNonQuery();
        }
        catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine($"[InkHistorySqlite] write failed: {ex.GetType().Name} - {ex.Message}");
        }
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

    private static (string? StrokesJson, DateTime? UpdatedAtUtc) ResolveEffectiveSnapshot(
        InkHistoryLoadResult bridge,
        InkSnapshot? sqlite)
    {
        var bridgeJson = string.IsNullOrWhiteSpace(bridge.StrokesJson)
            ? null
            : bridge.StrokesJson;
        if (bridgeJson is null)
        {
            // An empty sidecar is an explicit deletion. Do not resurrect a stale SQLite row.
            return (null, bridge.UpdatedAtUtc);
        }

        var sqliteJson = sqlite?.StrokesJson;
        if (string.IsNullOrWhiteSpace(sqliteJson))
        {
            return (bridgeJson, bridge.UpdatedAtUtc);
        }

        if (bridge.UpdatedAtUtc.HasValue && sqlite!.UpdatedAtUtc.HasValue)
        {
            return bridge.UpdatedAtUtc.Value >= sqlite.UpdatedAtUtc.Value
                ? (bridgeJson, bridge.UpdatedAtUtc)
                : (sqliteJson, sqlite.UpdatedAtUtc);
        }

        // Preserve the historical SQLite preference when one of the sources has no
        // timestamp (for example, an older bridge implementation or database row).
        return bridge.UpdatedAtUtc.HasValue
            ? (bridgeJson, bridge.UpdatedAtUtc)
            : (sqliteJson, sqlite?.UpdatedAtUtc);
    }

    private static DateTime? ParseUpdatedAtUtc(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsed))
        {
            return null;
        }

        return parsed.ToUniversalTime();
    }

    private static void ThrowIfInvalidPageIndex(int pageIndex)
    {
        if (pageIndex <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageIndex),
                pageIndex,
                "Page index must be greater than zero.");
        }
    }
}
