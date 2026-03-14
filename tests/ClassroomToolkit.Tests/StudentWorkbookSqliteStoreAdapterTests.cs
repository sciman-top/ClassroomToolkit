using ClassroomToolkit.Domain.Models;
using ClassroomToolkit.Infra.Storage;
using FluentAssertions;
using Microsoft.Data.Sqlite;
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
    public void LoadOrCreate_ShouldPreferSqliteState_WhenSqliteHasData()
    {
        var workbook = CreateWorkbook();
        var bridge = new FakeStudentWorkbookStoreBridge(new StudentWorkbookLoadResult(workbook, CreatedTemplate: false, RollStateJson: "{\"from\":\"excel\"}"));
        var dbPath = CreateTempDbPath();
        SeedSqliteState(dbPath, "{\"from\":\"sqlite\"}");
        var adapter = new StudentWorkbookSqliteStoreAdapter(bridge, _ => dbPath);

        var actual = adapter.LoadOrCreate("students.xlsx");

        bridge.LoadCalls.Should().Be(1);
        actual.RollStateJson.Should().Be("{\"from\":\"sqlite\"}");
        actual.Workbook.Should().BeSameAs(workbook);
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

    private static void SeedSqliteState(string dbPath, string? state)
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
                    updated_at_utc TEXT NOT NULL
                );
                """;
            create.ExecuteNonQuery();
        }

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO student_workbook_state(id, roll_state_json, updated_at_utc)
            VALUES(1, $json, $updated)
            ON CONFLICT(id) DO UPDATE SET
                roll_state_json = excluded.roll_state_json,
                updated_at_utc = excluded.updated_at_utc;
            """;
        command.Parameters.AddWithValue("$json", state ?? string.Empty);
        command.Parameters.AddWithValue("$updated", DateTime.UtcNow.ToString("O"));
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
}
