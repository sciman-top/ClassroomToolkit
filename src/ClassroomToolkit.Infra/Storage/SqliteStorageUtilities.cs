using System;
using System.Diagnostics.CodeAnalysis;
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

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath
        }.ToString();
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        return connection;
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "SQLite identifiers and the column definition are validated before schema SQL is constructed.")]
    internal static void EnsureColumnExists(
        SqliteConnection connection,
        string tableName,
        string columnName,
        string columnDefinition)
    {
        var quotedTableName = QuoteSqlIdentifier(tableName, nameof(tableName));
        var quotedColumnName = QuoteSqlIdentifier(columnName, nameof(columnName));
        var safeColumnDefinition = ValidateSqlColumnDefinition(columnDefinition, nameof(columnDefinition));
        if (HasColumn(connection, tableName, columnName))
        {
            return;
        }

        using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE {quotedTableName} ADD COLUMN {quotedColumnName} {safeColumnDefinition};";
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

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "SQLite table name is validated and quoted before PRAGMA schema SQL is constructed.")]
    private static bool HasColumn(SqliteConnection connection, string tableName, string columnName)
    {
        var quotedTableName = QuoteSqlIdentifier(tableName, nameof(tableName));
        using var pragma = connection.CreateCommand();
        pragma.CommandText = $"PRAGMA table_info({quotedTableName});";
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

    private static string QuoteSqlIdentifier(string identifier, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier, parameterName);

        if (!IsSqlIdentifier(identifier))
        {
            throw new ArgumentException("Value must be a simple SQLite identifier.", parameterName);
        }

        return "\"" + identifier + "\"";
    }

    private static string ValidateSqlColumnDefinition(string columnDefinition, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(columnDefinition, parameterName);

        foreach (var ch in columnDefinition)
        {
            if (!char.IsAsciiLetterOrDigit(ch)
                && ch != ' '
                && ch != '_'
                && ch != '('
                && ch != ')')
            {
                throw new ArgumentException("Value must be a simple SQLite column definition.", parameterName);
            }
        }

        return columnDefinition;
    }

    private static bool IsSqlIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (!char.IsAsciiLetter(value[0]) && value[0] != '_')
        {
            return false;
        }

        for (var index = 1; index < value.Length; index++)
        {
            var ch = value[index];
            if (!char.IsAsciiLetterOrDigit(ch) && ch != '_')
            {
                return false;
            }
        }

        return true;
    }
}
