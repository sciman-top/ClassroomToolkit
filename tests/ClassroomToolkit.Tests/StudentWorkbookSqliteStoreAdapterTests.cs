using ClassroomToolkit.Domain.Models;
using ClassroomToolkit.Domain.Serialization;
using ClassroomToolkit.Infra.Storage;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using System.Reflection;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class StudentWorkbookSqliteStoreAdapterTests
{
    [Fact]
    public void Constructor_ShouldThrow_WhenBridgeIsNull()
    {
        Action act = () => _ = new StudentWorkbookSqliteStoreAdapter(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void LoadOrCreate_ShouldThrowArgumentException_WhenPathIsBlank()
    {
        var adapter = new StudentWorkbookSqliteStoreAdapter(new FakeStudentWorkbookStoreBridge(
            new StudentWorkbookLoadResult(CreateWorkbook(), CreatedTemplate: false, RollStateJson: null)));

        var act = () => adapter.LoadOrCreate(" ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void LoadOrCreate_ShouldPreferBridgeState_WhenBridgeAndSqliteBothHaveData()
    {
        var workbook = CreateWorkbook();
        var bridge = new FakeStudentWorkbookStoreBridge(new StudentWorkbookLoadResult(workbook, CreatedTemplate: false, RollStateJson: "{\"from\":\"excel\"}"));
        var dbPath = CreateTempDbPath();
        var workbookPath = TestPathHelper.CreateFilePath("ctool_student_workbook_missing", ".xlsx");
        SeedSqliteState(dbPath, "{\"from\":\"sqlite\"}");
        var adapter = new StudentWorkbookSqliteStoreAdapter(bridge, _ => dbPath);

        var actual = adapter.LoadOrCreate(workbookPath);

        bridge.LoadCalls.Should().Be(1);
        actual.RollStateJson.Should().Be("{\"from\":\"excel\"}");
        actual.Workbook.Should().BeSameAs(workbook);
    }

    [Fact]
    public void LoadOrCreate_ShouldPreferSqliteState_WhenSqliteRevisionIsNewer()
    {
        var workbook = CreateWorkbook();
        var authorityJson = CreateVersionedRollStateJson(revision: 100, updatedAtUtc: new DateTime(2026, 3, 16, 8, 0, 0, DateTimeKind.Utc), currentStudent: "excel");
        var cacheJson = CreateVersionedRollStateJson(revision: 200, updatedAtUtc: new DateTime(2026, 3, 16, 7, 0, 0, DateTimeKind.Utc), currentStudent: "sqlite");
        var bridge = new FakeStudentWorkbookStoreBridge(new StudentWorkbookLoadResult(workbook, CreatedTemplate: false, RollStateJson: authorityJson));
        var dbPath = CreateTempDbPath();
        SeedSqliteState(dbPath, cacheJson, new DateTime(2026, 3, 16, 7, 0, 0, DateTimeKind.Utc), revision: 200);
        var adapter = new StudentWorkbookSqliteStoreAdapter(bridge, _ => dbPath);

        var actual = adapter.LoadOrCreate(TestPathHelper.CreateFilePath("ctool_student_workbook_missing", ".xlsx"));
        var states = RollStateSerializer.DeserializeWorkbookStates(actual.RollStateJson);

        states["班级1"].CurrentStudent.Should().Be("sqlite");
    }

    [Fact]
    public void LoadOrCreate_ShouldPreferBridgeState_WhenBridgeRevisionIsNewer()
    {
        var workbook = CreateWorkbook();
        var authorityJson = CreateVersionedRollStateJson(revision: 300, updatedAtUtc: new DateTime(2026, 3, 16, 8, 0, 0, DateTimeKind.Utc), currentStudent: "excel");
        var cacheJson = CreateVersionedRollStateJson(revision: 200, updatedAtUtc: new DateTime(2026, 3, 16, 9, 0, 0, DateTimeKind.Utc), currentStudent: "sqlite");
        var bridge = new FakeStudentWorkbookStoreBridge(new StudentWorkbookLoadResult(workbook, CreatedTemplate: false, RollStateJson: authorityJson));
        var dbPath = CreateTempDbPath();
        SeedSqliteState(dbPath, cacheJson, new DateTime(2026, 3, 16, 9, 0, 0, DateTimeKind.Utc), revision: 200);
        var adapter = new StudentWorkbookSqliteStoreAdapter(bridge, _ => dbPath);

        var actual = adapter.LoadOrCreate(TestPathHelper.CreateFilePath("ctool_student_workbook_missing", ".xlsx"));
        var states = RollStateSerializer.DeserializeWorkbookStates(actual.RollStateJson);

        states["班级1"].CurrentStudent.Should().Be("excel");
    }

    [Fact]
    public void LoadOrCreate_ShouldFallbackToSqliteState_WhenBridgeStateMissing()
    {
        var workbook = CreateWorkbook();
        var bridge = new FakeStudentWorkbookStoreBridge(new StudentWorkbookLoadResult(workbook, CreatedTemplate: false, RollStateJson: null));
        var dbPath = CreateTempDbPath();
        SeedSqliteState(dbPath, "{\"from\":\"sqlite\"}");
        var adapter = new StudentWorkbookSqliteStoreAdapter(bridge, _ => dbPath);

        var actual = adapter.LoadOrCreate("students.xlsx");

        actual.RollStateJson.Should().Be("{\"from\":\"sqlite\"}");
    }

    [Fact]
    public void LoadOrCreate_ShouldPreferSqliteState_WhenSqliteStateIsNewerAndBothHaveTimestamps()
    {
        var workbook = CreateWorkbook();
        var bridge = new FakeStudentWorkbookStoreBridge(new StudentWorkbookLoadResult(workbook, CreatedTemplate: false, RollStateJson: "{\"from\":\"excel\"}"));
        var dbPath = CreateTempDbPath();
        var workbookPath = CreateTempWorkbookPath();
        var authorityTimestamp = new DateTime(2026, 3, 16, 8, 0, 0, DateTimeKind.Utc);
        var sqliteTimestamp = authorityTimestamp.AddMinutes(5);
        File.SetLastWriteTimeUtc(workbookPath, authorityTimestamp);
        SeedSqliteState(dbPath, "{\"from\":\"sqlite\"}", sqliteTimestamp);
        var adapter = new StudentWorkbookSqliteStoreAdapter(bridge, _ => dbPath);

        var actual = adapter.LoadOrCreate(workbookPath);

        actual.RollStateJson.Should().Be("{\"from\":\"sqlite\"}");
    }

    [Fact]
    public void LoadOrCreate_ShouldPreferBridgeState_WhenBridgeStateIsNewerAndBothHaveTimestamps()
    {
        var workbook = CreateWorkbook();
        var bridge = new FakeStudentWorkbookStoreBridge(new StudentWorkbookLoadResult(workbook, CreatedTemplate: false, RollStateJson: "{\"from\":\"excel\"}"));
        var dbPath = CreateTempDbPath();
        var workbookPath = CreateTempWorkbookPath();
        var authorityTimestamp = new DateTime(2026, 3, 16, 8, 0, 0, DateTimeKind.Utc);
        var sqliteTimestamp = authorityTimestamp.AddMinutes(-5);
        File.SetLastWriteTimeUtc(workbookPath, authorityTimestamp);
        SeedSqliteState(dbPath, "{\"from\":\"sqlite\"}", sqliteTimestamp);
        var adapter = new StudentWorkbookSqliteStoreAdapter(bridge, _ => dbPath);

        var actual = adapter.LoadOrCreate(workbookPath);

        actual.RollStateJson.Should().Be("{\"from\":\"excel\"}");
    }

    [Fact]
    public void Save_ShouldDelegateAndPersist_ToSqlite()
    {
        var workbook = CreateWorkbook();
        var bridge = new FakeStudentWorkbookStoreBridge(new StudentWorkbookLoadResult(workbook, CreatedTemplate: false, RollStateJson: null));
        var dbPath = CreateTempDbPath();
        var adapter = new StudentWorkbookSqliteStoreAdapter(bridge, _ => dbPath);

        adapter.Save(workbook, "students.xlsx", "{\"state\":1}");

        bridge.SaveCalls.Should().Be(1);
        bridge.LastSavePath.Should().Be("students.xlsx");
        bridge.LastSavedWorkbook.Should().BeSameAs(workbook);
        bridge.LastRollStateJson.Should().Be("{\"state\":1}");
        ReadSqliteState(dbPath).Should().Be("{\"state\":1}");
    }

    [Fact]
    public void Save_ShouldThrowArgumentNullException_WhenWorkbookIsNull()
    {
        var adapter = new StudentWorkbookSqliteStoreAdapter(new FakeStudentWorkbookStoreBridge(
            new StudentWorkbookLoadResult(CreateWorkbook(), CreatedTemplate: false, RollStateJson: null)));

        var act = () => adapter.Save(null!, "students.xlsx", "{\"state\":1}");

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Save_ShouldThrowArgumentException_WhenPathIsBlank()
    {
        var adapter = new StudentWorkbookSqliteStoreAdapter(new FakeStudentWorkbookStoreBridge(
            new StudentWorkbookLoadResult(CreateWorkbook(), CreatedTemplate: false, RollStateJson: null)));

        var act = () => adapter.Save(CreateWorkbook(), " ", "{\"state\":1}");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ResolveDbPath_ShouldFallback_WhenWorkbookPathIsInvalid()
    {
        var method = typeof(StudentWorkbookSqliteStoreAdapter).GetMethod(
            "ResolveDbPath",
            BindingFlags.Static | BindingFlags.NonPublic);
        method.Should().NotBeNull();

        var act = () => (string)method!.Invoke(null, ["\0invalid-path"])!;

        var dbPath = act.Should().NotThrow().Subject;
        dbPath.Should().Be(Path.Combine(AppContext.BaseDirectory, "students.studentworkbook.sqlite3"));
    }

    [Fact]
    public void LoadOrCreate_ShouldFallbackToSqliteSnapshot_WhenBridgeThrows()
    {
        var workbook = new StudentWorkbook(
            new Dictionary<string, ClassRoster>
            {
                ["高一1班"] = new ClassRoster(
                    "高一1班",
                    new[]
                    {
                        StudentRecord.Create("001", "张三", "高一1班", "一组", rowId: "row-1"),
                        StudentRecord.Create("002", "李四", "高一1班", "二组", rowId: "row-2")
                    })
            },
            "高一1班");

        var dbPath = CreateTempDbPath();
        var seedingBridge = new FakeStudentWorkbookStoreBridge(new StudentWorkbookLoadResult(workbook, CreatedTemplate: false, RollStateJson: "{\"seed\":1}"));
        var seedingAdapter = new StudentWorkbookSqliteStoreAdapter(seedingBridge, _ => dbPath);
        seedingAdapter.Save(workbook, "students.xlsx", "{\"seed\":1}");

        var failingBridge = new ThrowingStudentWorkbookStoreBridge();
        var adapter = new StudentWorkbookSqliteStoreAdapter(failingBridge, _ => dbPath);

        var loaded = adapter.LoadOrCreate("students.xlsx");

        loaded.CreatedTemplate.Should().BeFalse();
        loaded.RollStateJson.Should().Be("{\"seed\":1}");
        loaded.Workbook.ClassNames.Should().Contain("高一1班");
        loaded.Workbook.GetActiveRoster().Students.Should().HaveCount(2);
    }

    [Fact]
    public void LoadOrCreate_ShouldRethrow_WhenBridgeThrowsFatalException()
    {
        var dbPath = CreateTempDbPath();
        SeedSqliteState(dbPath, "{\"seed\":1}");
        var adapter = new StudentWorkbookSqliteStoreAdapter(new FatalThrowingStudentWorkbookStoreBridge(), _ => dbPath);

        Action act = () => _ = adapter.LoadOrCreate("students.xlsx");

        act.Should().Throw<AccessViolationException>();
    }

    [Fact]
    public void LoadOrCreate_ShouldRecoverMalformedSnapshotStudentClassAndRowId_WhenBridgeThrows()
    {
        var dbPath = CreateTempDbPath();
        SeedStudentWorkbookSnapshot(dbPath,
            """
            {"activeClass":"高一1班","classes":[{"className":"高一1班","columnOrder":["学号","姓名","班级","分组","__row_id__"],"students":[{"studentId":"001","name":"张三","className":"","groupName":"一组","rowId":" ","rowKey":"rk:x","extraFields":{}}]}]}
            """);
        var adapter = new StudentWorkbookSqliteStoreAdapter(new ThrowingStudentWorkbookStoreBridge(), _ => dbPath);

        var loaded = adapter.LoadOrCreate("students.xlsx");
        var student = loaded.Workbook.GetActiveRoster().Students.Single();

        student.ClassName.Should().Be("高一1班");
        student.RowId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Save_ShouldFallbackToDefaultPath_WhenResolverReturnsBlank()
    {
        var workbook = CreateWorkbook();
        var bridge = new FakeStudentWorkbookStoreBridge(new StudentWorkbookLoadResult(workbook, CreatedTemplate: false, RollStateJson: null));
        var tempDir = TestPathHelper.CreateDirectory("ctool_student_workbook_sqlite_fallback_blank");
        var workbookPath = Path.Combine(tempDir, "students.xlsx");
        var adapter = new StudentWorkbookSqliteStoreAdapter(bridge, _ => " ");

        Action act = () => adapter.Save(workbook, workbookPath, "{\"fallback\":1}");

        act.Should().NotThrow();
        var fallbackDbPath = Path.Combine(tempDir, "students.studentworkbook.sqlite3");
        File.Exists(fallbackDbPath).Should().BeTrue();
        ReadSqliteState(fallbackDbPath).Should().Be("{\"fallback\":1}");
    }

    [Fact]
    public void Save_ShouldFallbackToDefaultPath_WhenResolverThrowsNonFatal()
    {
        var workbook = CreateWorkbook();
        var bridge = new FakeStudentWorkbookStoreBridge(new StudentWorkbookLoadResult(workbook, CreatedTemplate: false, RollStateJson: null));
        var tempDir = TestPathHelper.CreateDirectory("ctool_student_workbook_sqlite_fallback_throw");
        var workbookPath = Path.Combine(tempDir, "students.xlsx");
        var adapter = new StudentWorkbookSqliteStoreAdapter(bridge, _ => throw new IOException("resolver-failure"));

        Action act = () => adapter.Save(workbook, workbookPath, "{\"fallback\":2}");

        act.Should().NotThrow();
        var fallbackDbPath = Path.Combine(tempDir, "students.studentworkbook.sqlite3");
        File.Exists(fallbackDbPath).Should().BeTrue();
        ReadSqliteState(fallbackDbPath).Should().Be("{\"fallback\":2}");
    }

    [Fact]
    public void Save_ShouldPersist_WhenSqlitePathContainsConnectionStringSeparator()
    {
        var workbook = CreateWorkbook();
        var bridge = new FakeStudentWorkbookStoreBridge(new StudentWorkbookLoadResult(workbook, CreatedTemplate: false, RollStateJson: null));
        var dbDirectory = TestPathHelper.CreateDirectory("ctool_student_workbook_sqlite_semicolon");
        var dbPath = Path.Combine(dbDirectory, "students;studentworkbook.sqlite3");
        var adapter = new StudentWorkbookSqliteStoreAdapter(bridge, _ => dbPath);

        adapter.Save(workbook, "students.xlsx", "{\"separator\":1}");

        File.Exists(dbPath).Should().BeTrue();
        ReadSqliteStateWithBuilder(dbPath).Should().Be("{\"separator\":1}");
    }

    private static StudentWorkbook CreateWorkbook()
    {
        return new StudentWorkbook(
            new Dictionary<string, ClassRoster>
            {
                ["班级1"] = new ClassRoster("班级1", Array.Empty<StudentRecord>())
            },
            "班级1");
    }

    private static string CreateTempDbPath()
    {
        var dir = TestPathHelper.CreateDirectory("ctool_student_workbook_sqlite");
        return Path.Combine(dir, "students.studentworkbook.sqlite3");
    }

    private static string CreateTempWorkbookPath()
    {
        var path = TestPathHelper.CreateFilePath("ctool_student_workbook", ".xlsx");
        File.WriteAllText(path, "stub");
        return path;
    }

    private static void SeedSqliteState(string dbPath, string? state, DateTime? updatedAtUtc = null, long? revision = null)
    {
        var connectionString = $"Data Source={dbPath}";
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        using (var create = connection.CreateCommand())
        {
            create.CommandText =
                """
                CREATE TABLE IF NOT EXISTS student_workbook_state
                (
                    id INTEGER PRIMARY KEY CHECK (id = 1),
                    roll_state_json TEXT NULL,
                    revision INTEGER NOT NULL DEFAULT 0,
                    updated_at_utc TEXT NOT NULL
                );
                """;
            create.ExecuteNonQuery();
        }

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO student_workbook_state(id, roll_state_json, revision, updated_at_utc)
            VALUES(1, $json, $revision, $updated)
            ON CONFLICT(id) DO UPDATE SET
                roll_state_json = excluded.roll_state_json,
                revision = excluded.revision,
                updated_at_utc = excluded.updated_at_utc;
            """;
        command.Parameters.AddWithValue("$json", state ?? string.Empty);
        command.Parameters.AddWithValue("$revision", revision ?? 0L);
        command.Parameters.AddWithValue("$updated", (updatedAtUtc ?? DateTime.UtcNow).ToString("O"));
        command.ExecuteNonQuery();
    }

    private static string? ReadSqliteState(string dbPath)
    {
        if (!File.Exists(dbPath))
        {
            return null;
        }

        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT roll_state_json FROM student_workbook_state WHERE id = 1 LIMIT 1;";
        var scalar = command.ExecuteScalar();
        return scalar as string;
    }

    private static string? ReadSqliteStateWithBuilder(string dbPath)
    {
        if (!File.Exists(dbPath))
        {
            return null;
        }

        using var connection = new SqliteConnection(BuildSqliteConnectionString(dbPath));
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT roll_state_json FROM student_workbook_state WHERE id = 1 LIMIT 1;";
        var scalar = command.ExecuteScalar();
        return scalar as string;
    }

    private static void SeedStudentWorkbookSnapshot(string dbPath, string workbookJson)
    {
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        using (var create = connection.CreateCommand())
        {
            create.CommandText =
                """
                CREATE TABLE IF NOT EXISTS student_workbook_snapshot
                (
                    id INTEGER PRIMARY KEY CHECK (id = 1),
                    workbook_json TEXT NOT NULL,
                    updated_at_utc TEXT NOT NULL
                );
                """;
            create.ExecuteNonQuery();
        }

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO student_workbook_snapshot(id, workbook_json, updated_at_utc)
            VALUES(1, $workbook, $updated)
            ON CONFLICT(id) DO UPDATE SET
                workbook_json = excluded.workbook_json,
                updated_at_utc = excluded.updated_at_utc;
            """;
        command.Parameters.AddWithValue("$workbook", workbookJson);
        command.Parameters.AddWithValue("$updated", DateTime.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }

    private static string BuildSqliteConnectionString(string dbPath)
    {
        return new SqliteConnectionStringBuilder
        {
            DataSource = dbPath
        }.ToString();
    }

    private static string CreateVersionedRollStateJson(long revision, DateTime updatedAtUtc, string currentStudent)
    {
        return RollStateSerializer.SerializeWorkbookStates(
            new Dictionary<string, ClassRollState>
            {
                ["班级1"] = new ClassRollState
                {
                    CurrentGroup = "一组",
                    CurrentStudent = currentStudent
                }
            },
            revision,
            updatedAtUtc);
    }

    private sealed class FakeStudentWorkbookStoreBridge : IStudentWorkbookStoreBridge
    {
        private readonly StudentWorkbookLoadResult _loadResult;

        public FakeStudentWorkbookStoreBridge(StudentWorkbookLoadResult loadResult)
        {
            _loadResult = loadResult;
        }

        public int LoadCalls { get; private set; }
        public int SaveCalls { get; private set; }
        public string? LastSavePath { get; private set; }
        public StudentWorkbook? LastSavedWorkbook { get; private set; }
        public string? LastRollStateJson { get; private set; }

        public StudentWorkbookLoadResult LoadOrCreate(string path)
        {
            LoadCalls++;
            return _loadResult;
        }

        public void Save(StudentWorkbook workbook, string path, string? rollStateJson)
        {
            SaveCalls++;
            LastSavedWorkbook = workbook;
            LastSavePath = path;
            LastRollStateJson = rollStateJson;
        }
    }

    private sealed class ThrowingStudentWorkbookStoreBridge : IStudentWorkbookStoreBridge
    {
        public StudentWorkbookLoadResult LoadOrCreate(string path)
        {
            throw new IOException("bridge-failure");
        }

        public void Save(StudentWorkbook workbook, string path, string? rollStateJson)
        {
            throw new IOException("bridge-failure");
        }
    }

    private sealed class FatalThrowingStudentWorkbookStoreBridge : IStudentWorkbookStoreBridge
    {
        public StudentWorkbookLoadResult LoadOrCreate(string path)
        {
            throw new AccessViolationException("fatal-bridge-failure");
        }

        public void Save(StudentWorkbook workbook, string path, string? rollStateJson)
        {
            throw new AccessViolationException("fatal-bridge-failure");
        }
    }
}
