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
                File.Delete(tempPath);
            }
        }
    }
}
