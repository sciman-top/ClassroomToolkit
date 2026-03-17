using ClassroomToolkit.Domain.Models;
using ClassroomToolkit.Infra.Storage;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class StudentWorkbookStoreTests
{
    [Fact]
    public void SaveAndLoad_ShouldPreserveStudentsAndRollState()
    {
        var tempPath = TestPathHelper.CreateFilePath("ctool_workbook", ".xlsx");
        try
        {
            var students = new List<StudentRecord>
            {
                StudentRecord.Create("1001", "张三", "A班", "一组"),
                StudentRecord.Create("1002", "李四", "A班", "二组"),
            };
            var roster = new ClassRoster("A班", students);
            var workbook = new StudentWorkbook(new Dictionary<string, ClassRoster> { ["A班"] = roster }, "A班");
            var store = new StudentWorkbookStore();
            var rollStateJson = "{\"version\":\"2.0\"}";

            store.Save(workbook, tempPath, rollStateJson);
            var loaded = store.LoadOrCreate(tempPath);

            loaded.Workbook.ClassNames.Should().Contain("A班");
            loaded.Workbook.GetActiveRoster().Students.Should().HaveCount(2);
            loaded.RollStateJson.Should().Be(rollStateJson);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch (IOException)
                {
                }
            }
        }
    }

    [Fact]
    public void Save_ShouldThrowArgumentNullException_WhenWorkbookIsNull()
    {
        var store = new StudentWorkbookStore();
        var tempPath = TestPathHelper.CreateFilePath("ctool_workbook_null", ".xlsx");

        var act = () => store.Save(null!, tempPath, rollStateJson: null);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void LoadOrCreate_ShouldThrowArgumentException_WhenPathIsBlank()
    {
        var store = new StudentWorkbookStore();

        var act = () => store.LoadOrCreate(" ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Save_ShouldUseBestEffortTempCleanup()
    {
        var source = File.ReadAllText(GetStoreSourcePath());

        source.Should().Contain("Best-effort cleanup for temp workbook files.");
        source.Should().Contain("catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))");
    }

    [Fact]
    public void LoadOrCreate_ShouldFallbackToTemplate_WhenWorkbookFileIsCorrupted()
    {
        var tempPath = TestPathHelper.CreateFilePath("ctool_workbook_corrupt", ".xlsx");
        try
        {
            File.WriteAllText(tempPath, "not-an-xlsx");
            var store = new StudentWorkbookStore();

            var loaded = store.LoadOrCreate(tempPath);

            loaded.CreatedTemplate.Should().BeFalse();
            loaded.Workbook.ClassNames.Should().Contain("班级1");
            loaded.Workbook.GetActiveRoster().Students.Should().NotBeEmpty();
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch (IOException)
                {
                }
            }
        }
    }

    private static string GetStoreSourcePath()
    {
        return Path.Combine(
            FindRepositoryRoot(new DirectoryInfo(AppContext.BaseDirectory))!.FullName,
            "src",
            "ClassroomToolkit.Infra",
            "Storage",
            "StudentWorkbookStore.cs");
    }

    private static DirectoryInfo? FindRepositoryRoot(DirectoryInfo? start)
    {
        var current = start;
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "ClassroomToolkit.sln")))
            {
                return current;
            }

            current = current.Parent;
        }

        return null;
    }
}
