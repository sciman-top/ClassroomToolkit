using System.IO;
using System.Reflection;
using ClassroomToolkit.Application.Abstractions;
using ClassroomToolkit.Infra.Storage;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class InkHistorySqliteStoreAdapterTests
{
    [Fact]
    public void Constructor_ShouldThrow_WhenBridgeIsNull()
    {
        Action act = () => _ = new InkHistorySqliteStoreAdapter(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void LoadOrCreate_ShouldPreferSqliteSnapshot_WhenSqliteHasData()
    {
        var bridge = new FakeInkHistoryStoreBridge(new InkHistoryLoadResult("lesson-a.pptx", 1, "[{\"from\":\"bridge\"}]", CreatedTemplate: false));
        var dbPath = CreateTempDbPath();
        SeedSqliteSnapshot(dbPath, "lesson-a.pptx", 1, "[{\"from\":\"sqlite\"}]");
        var adapter = new InkHistorySqliteStoreAdapter(bridge, _ => dbPath);

        var actual = adapter.LoadOrCreate("lesson-a.pptx", 1);

        bridge.LoadCalls.Should().Be(1);
        actual.StrokesJson.Should().Be("[{\"from\":\"sqlite\"}]");
        actual.SourcePath.Should().Be("lesson-a.pptx");
        actual.PageIndex.Should().Be(1);
    }

    [Fact]
    public void LoadOrCreate_ShouldThrowArgumentException_WhenSourcePathIsBlank()
    {
        var adapter = new InkHistorySqliteStoreAdapter(
            new FakeInkHistoryStoreBridge(new InkHistoryLoadResult("lesson-a.pptx", 1, null, CreatedTemplate: false)));

        var act = () => adapter.LoadOrCreate(" ", 1);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void LoadOrCreate_ShouldThrowArgumentOutOfRangeException_WhenPageIndexIsNegative()
    {
        var adapter = new InkHistorySqliteStoreAdapter(
            new FakeInkHistoryStoreBridge(new InkHistoryLoadResult("lesson-a.pptx", 1, null, CreatedTemplate: false)));

        var act = () => adapter.LoadOrCreate("lesson-a.pptx", -1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Save_ShouldDelegateAndPersist_ToSqlite()
    {
        var bridge = new FakeInkHistoryStoreBridge(new InkHistoryLoadResult("lesson-b.pptx", 2, null, CreatedTemplate: false));
        var dbPath = CreateTempDbPath();
        var adapter = new InkHistorySqliteStoreAdapter(bridge, _ => dbPath);

        adapter.Save("lesson-b.pptx", 2, "[{\"state\":1}]");

        bridge.SaveCalls.Should().Be(1);
        bridge.LastSaveSourcePath.Should().Be("lesson-b.pptx");
        bridge.LastSavePageIndex.Should().Be(2);
        bridge.LastSavedStrokesJson.Should().Be("[{\"state\":1}]");
        ReadSqliteSnapshot(dbPath, "lesson-b.pptx", 2).Should().Be("[{\"state\":1}]");
    }

    [Fact]
    public void Save_ShouldThrowArgumentException_WhenSourcePathIsBlank()
    {
        var adapter = new InkHistorySqliteStoreAdapter(
            new FakeInkHistoryStoreBridge(new InkHistoryLoadResult("lesson-b.pptx", 2, null, CreatedTemplate: false)));

        var act = () => adapter.Save(" ", 2, "[{\"state\":1}]");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Save_ShouldThrowArgumentOutOfRangeException_WhenPageIndexIsNegative()
    {
        var adapter = new InkHistorySqliteStoreAdapter(
            new FakeInkHistoryStoreBridge(new InkHistoryLoadResult("lesson-b.pptx", 2, null, CreatedTemplate: false)));

        var act = () => adapter.Save("lesson-b.pptx", -1, "[{\"state\":1}]");

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void LoadOrCreate_ShouldFallbackToSqliteSnapshot_WhenBridgeThrows()
    {
        var dbPath = CreateTempDbPath();
        var seedingBridge = new FakeInkHistoryStoreBridge(new InkHistoryLoadResult("lesson-c.pdf", 3, "[{\"seed\":1}]", CreatedTemplate: false));
        var seedingAdapter = new InkHistorySqliteStoreAdapter(seedingBridge, _ => dbPath);
        seedingAdapter.Save("lesson-c.pdf", 3, "[{\"seed\":1}]");

        var adapter = new InkHistorySqliteStoreAdapter(new ThrowingInkHistoryStoreBridge(), _ => dbPath);

        var actual = adapter.LoadOrCreate("lesson-c.pdf", 3);

        actual.CreatedTemplate.Should().BeFalse();
        actual.StrokesJson.Should().Be("[{\"seed\":1}]");
        actual.SourcePath.Should().Be("lesson-c.pdf");
        actual.PageIndex.Should().Be(3);
    }

    [Fact]
    public void LoadOrCreate_ShouldNotWriteSqlite_WhenWriteSnapshotDisabled()
    {
        var bridge = new FakeInkHistoryStoreBridge(new InkHistoryLoadResult("lesson-d.pdf", 4, "[{\"from\":\"bridge\"}]", CreatedTemplate: false));
        var dbPath = CreateTempDbPath();
        var adapter = new InkHistorySqliteStoreAdapter(bridge, _ => dbPath);
        File.Exists(dbPath).Should().BeFalse();

        var actual = adapter.LoadOrCreate("lesson-d.pdf", 4, writeSnapshot: false);

        bridge.LoadCalls.Should().Be(1);
        actual.StrokesJson.Should().Be("[{\"from\":\"bridge\"}]");
        File.Exists(dbPath).Should().BeFalse();
    }

    [Fact]
    public void LoadOrCreate_ShouldRethrow_WhenBridgeThrowsFatalException()
    {
        var dbPath = CreateTempDbPath();
        SeedSqliteSnapshot(dbPath, "lesson-fatal.pptx", 1, "[{\"seed\":1}]");
        var adapter = new InkHistorySqliteStoreAdapter(new FatalThrowingInkHistoryStoreBridge(), _ => dbPath);

        Action act = () => _ = adapter.LoadOrCreate("lesson-fatal.pptx", 1);

        act.Should().Throw<AccessViolationException>();
    }

    [Fact]
    public void Save_ShouldFallbackToDefaultPath_WhenResolverReturnsBlank()
    {
        var bridge = new FakeInkHistoryStoreBridge(new InkHistoryLoadResult("lesson-e.pptx", 5, null, CreatedTemplate: false));
        var tempDir = TestPathHelper.CreateDirectory("ctool_ink_history_sqlite_fallback_blank");
        var sourcePath = Path.Combine(tempDir, "lesson-e.pptx");
        var adapter = new InkHistorySqliteStoreAdapter(bridge, _ => " ");

        Action act = () => adapter.Save(sourcePath, 5, "[{\"fallback\":1}]");

        act.Should().NotThrow();
        var fallbackDbPath = Path.Combine(tempDir, "lesson-e.inkhistory.sqlite3");
        File.Exists(fallbackDbPath).Should().BeTrue();
        ReadSqliteSnapshot(fallbackDbPath, sourcePath, 5).Should().Be("[{\"fallback\":1}]");
    }

    [Fact]
    public void Save_ShouldFallbackToDefaultPath_WhenResolverThrowsNonFatal()
    {
        var bridge = new FakeInkHistoryStoreBridge(new InkHistoryLoadResult("lesson-f.pptx", 6, null, CreatedTemplate: false));
        var tempDir = TestPathHelper.CreateDirectory("ctool_ink_history_sqlite_fallback_throw");
        var sourcePath = Path.Combine(tempDir, "lesson-f.pptx");
        var adapter = new InkHistorySqliteStoreAdapter(bridge, _ => throw new IOException("resolver-failure"));

        Action act = () => adapter.Save(sourcePath, 6, "[{\"fallback\":2}]");

        act.Should().NotThrow();
        var fallbackDbPath = Path.Combine(tempDir, "lesson-f.inkhistory.sqlite3");
        File.Exists(fallbackDbPath).Should().BeTrue();
        ReadSqliteSnapshot(fallbackDbPath, sourcePath, 6).Should().Be("[{\"fallback\":2}]");
    }

    [Fact]
    public void ResolveDbPath_ShouldFallback_WhenSourcePathIsInvalid()
    {
        var method = typeof(InkHistorySqliteStoreAdapter).GetMethod(
            "ResolveDbPath",
            BindingFlags.Static | BindingFlags.NonPublic);
        method.Should().NotBeNull();

        var act = () => (string)method!.Invoke(null, ["\0invalid-path"])!;

        var dbPath = act.Should().NotThrow().Subject;
        dbPath.Should().Be(Path.Combine(AppContext.BaseDirectory, "inkhistory.inkhistory.sqlite3"));
    }

    private static string CreateTempDbPath()
    {
        var dir = TestPathHelper.CreateDirectory("ctool_ink_history_sqlite");
        return Path.Combine(dir, "inkhistory.sqlite3");
    }

    private static void SeedSqliteSnapshot(string dbPath, string sourcePath, int pageIndex, string strokesJson)
    {
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        using (var create = connection.CreateCommand())
        {
            create.CommandText =
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
            create.ExecuteNonQuery();
        }

        using var insert = connection.CreateCommand();
        insert.CommandText =
            """
            INSERT INTO ink_history_snapshot(source_path, page_index, strokes_json, updated_at_utc)
            VALUES($sourcePath, $pageIndex, $strokes, $updatedAtUtc)
            ON CONFLICT(source_path, page_index) DO UPDATE SET
                strokes_json = excluded.strokes_json,
                updated_at_utc = excluded.updated_at_utc;
            """;
        insert.Parameters.AddWithValue("$sourcePath", sourcePath);
        insert.Parameters.AddWithValue("$pageIndex", pageIndex);
        insert.Parameters.AddWithValue("$strokes", strokesJson);
        insert.Parameters.AddWithValue("$updatedAtUtc", DateTime.UtcNow.ToString("O"));
        insert.ExecuteNonQuery();
    }

    private static string? ReadSqliteSnapshot(string dbPath, string sourcePath, int pageIndex)
    {
        if (!File.Exists(dbPath))
        {
            return null;
        }

        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

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

    private sealed class FakeInkHistoryStoreBridge : IInkHistoryStoreBridge
    {
        private readonly InkHistoryLoadResult _loadResult;

        public FakeInkHistoryStoreBridge(InkHistoryLoadResult loadResult)
        {
            _loadResult = loadResult;
        }

        public int LoadCalls { get; private set; }
        public int SaveCalls { get; private set; }
        public string? LastSaveSourcePath { get; private set; }
        public int LastSavePageIndex { get; private set; }
        public string? LastSavedStrokesJson { get; private set; }

        public InkHistoryLoadResult LoadOrCreate(string sourcePath, int pageIndex)
        {
            LoadCalls++;
            return _loadResult with
            {
                SourcePath = sourcePath,
                PageIndex = pageIndex
            };
        }

        public void Save(string sourcePath, int pageIndex, string? strokesJson)
        {
            SaveCalls++;
            LastSaveSourcePath = sourcePath;
            LastSavePageIndex = pageIndex;
            LastSavedStrokesJson = strokesJson;
        }
    }

    private sealed class ThrowingInkHistoryStoreBridge : IInkHistoryStoreBridge
    {
        public InkHistoryLoadResult LoadOrCreate(string sourcePath, int pageIndex)
        {
            throw new IOException("bridge-failure");
        }

        public void Save(string sourcePath, int pageIndex, string? strokesJson)
        {
            throw new IOException("bridge-failure");
        }
    }

    private sealed class FatalThrowingInkHistoryStoreBridge : IInkHistoryStoreBridge
    {
        public InkHistoryLoadResult LoadOrCreate(string sourcePath, int pageIndex)
        {
            throw new AccessViolationException("fatal-bridge-failure");
        }

        public void Save(string sourcePath, int pageIndex, string? strokesJson)
        {
            throw new AccessViolationException("fatal-bridge-failure");
        }
    }
}
