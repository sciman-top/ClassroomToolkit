using System;
using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;

namespace ClassroomToolkit.Infra.Storage;

internal static class SqliteStorageUtilities
{
    internal static SqliteConnection CreateOpenConnection(string dbPath)
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

    internal static void EnsureColumnExists(
        SqliteConnection connection,
        string tableName,
        string columnName,
        string columnDefinition)
    {
        if (HasColumn(connection, tableName, columnName))
        {
            return;
        }

        using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};";
        alter.ExecuteNonQuery();
    }

    internal static DateTime? TryParseUtcTimestamp(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (!DateTime.TryParse(
                raw,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsed))
        {
            return null;
        }

        return parsed.ToUniversalTime();
    }

    private static bool HasColumn(SqliteConnection connection, string tableName, string columnName)
    {
        using var pragma = connection.CreateCommand();
        pragma.CommandText = $"PRAGMA table_info('{tableName}');";
        using var reader = pragma.ExecuteReader();
        while (reader.Read())
        {
            var name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            if (string.Equals(name, columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

