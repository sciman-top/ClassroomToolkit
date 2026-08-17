using ClassroomToolkit.Domain.Models;
using ClassroomToolkit.Infra.Storage;
using FluentAssertions;
using ClosedXML.Excel;

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
    public void Save_ShouldNotLeaveTempFile_WhenTargetIsLocked()
    {
        var tempPath = TestPathHelper.CreateFilePath("ctool_workbook_locked", ".xlsx");
        try
        {
            var students = new List<StudentRecord>
            {
                StudentRecord.Create("1001", "张三", "A班", "一组"),
            };
            var roster = new ClassRoster("A班", students);
            var workbook = new StudentWorkbook(new Dictionary<string, ClassRoster> { ["A班"] = roster }, "A班");
            var store = new StudentWorkbookStore();
            store.Save(workbook, tempPath, "{\"version\":\"2.0\"}");

            using var lockStream = new FileStream(tempPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Action act = () => store.Save(workbook, tempPath, "{\"version\":\"3.0\"}");

            act.Should().Throw<Exception>().Where(ex =>
                ex.GetType() == typeof(IOException)
                || ex.GetType() == typeof(UnauthorizedAccessException));
            Directory.GetFiles(Path.GetDirectoryName(tempPath)!, $"{Path.GetFileName(tempPath)}.*.tmp.xlsx").Should().BeEmpty();
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
    public void LoadOrCreate_ShouldThrowPreserveOriginalAndBlockSave_WhenWorkbookFileIsCorrupted()
    {
        var tempPath = TestPathHelper.CreateFilePath("ctool_workbook_corrupt", ".xlsx");
        try
        {
            File.WriteAllText(tempPath, "not-an-xlsx");
            var originalBytes = File.ReadAllBytes(tempPath);
            var store = new StudentWorkbookStore();

            var act = () => store.LoadOrCreate(tempPath);

            act.Should().Throw<Exception>();
            File.ReadAllBytes(tempPath).Should().Equal(originalBytes);

            var fallbackRoster = new ClassRoster(
                "1班",
                [StudentRecord.Create("01", "恢复数据", "1班", "A")]);
            var fallbackWorkbook = new StudentWorkbook(
                new Dictionary<string, ClassRoster> { ["1班"] = fallbackRoster },
                "1班");
            var save = () => store.Save(fallbackWorkbook, tempPath, rollStateJson: null);

            save.Should().Throw<InvalidOperationException>();
            File.ReadAllBytes(tempPath).Should().Equal(originalBytes);
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
    public void LoadOrCreate_ShouldCreateDefaultTemplate_WhenWorkbookMissing()
    {
        var tempPath = TestPathHelper.CreateFilePath("ctool_workbook_missing", ".xlsx");
        try
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            var store = new StudentWorkbookStore();
            var loaded = store.LoadOrCreate(tempPath);

            loaded.CreatedTemplate.Should().BeTrue();
            loaded.Workbook.ClassNames.Should().ContainSingle().Which.Should().Be("1班");
            loaded.Workbook.GetActiveRoster().Students.Should().HaveCount(3);
            loaded.Workbook.GetActiveRoster().Students.Select(s => s.StudentId).Should().ContainInOrder("01", "02", "03");

            using var workbook = new XLWorkbook(tempPath);
            workbook.Worksheets.Any(s => s.Name == "1班").Should().BeTrue();
            workbook.Worksheets.Any(s => s.Name == StudentWorkbookStore.RollStateSheetName).Should().BeTrue();
            var classSheet = workbook.Worksheet("1班");
            classSheet.Cell(1, 1).GetString().Should().Be("学号");
            classSheet.Cell(1, 2).GetString().Should().Be("姓名");
            classSheet.Cell(1, 3).GetString().Should().Be("分组");
            classSheet.Cell(1, 4).GetString().Should().Be(ClassRoster.InternalRowIdColumn);
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
    public void LoadOrCreate_ShouldRepairColumnsAndRollStateSheet_WhenWorkbookFormatIsInvalid()
    {
        var tempPath = TestPathHelper.CreateFilePath("ctool_workbook_repair", ".xlsx");
        var backupPattern = $"{Path.GetFileNameWithoutExtension(tempPath)}.bak-normalize-*{Path.GetExtension(tempPath)}";
        try
        {
            using (var workbook = new XLWorkbook())
            {
                var sheet = workbook.Worksheets.Add("1班");
                sheet.Cell(1, 1).Value = "姓名";
                sheet.Cell(1, 2).Value = "学号";
                sheet.Cell(1, 3).Value = "班级";
                sheet.Cell(2, 1).Value = "张三";
                sheet.Cell(2, 2).Value = "01";
                sheet.Cell(2, 3).Value = "1班";
                sheet.Cell(5, 8).FormulaA1 = "1+1";
                sheet.Cell(5, 8).Style.Fill.BackgroundColor = XLColor.Yellow;
                workbook.SaveAs(tempPath);
            }
            var originalBytes = File.ReadAllBytes(tempPath);

            var store = new StudentWorkbookStore();
            var loaded = store.LoadOrCreate(tempPath);

            loaded.Workbook.ClassNames.Should().Contain("1班");
            loaded.Workbook.GetActiveRoster().Students.Should().ContainSingle();

            using var repairedWorkbook = new XLWorkbook(tempPath);
            repairedWorkbook.Worksheets.Any(s => s.Name == StudentWorkbookStore.RollStateSheetName).Should().BeTrue();
            var classSheet = repairedWorkbook.Worksheet("1班");
            classSheet.Cell(1, 1).GetString().Should().Be("学号");
            classSheet.Cell(1, 2).GetString().Should().Be("姓名");
            classSheet.Cell(1, 3).GetString().Should().Be("分组");
            classSheet.Cell(1, 4).GetString().Should().Be(ClassRoster.InternalRowIdColumn);
            classSheet.Cell(2, 1).GetString().Should().Be("01");
            classSheet.Cell(2, 2).GetString().Should().Be("张三");
            repairedWorkbook.Worksheet(StudentWorkbookStore.RollStateSheetName).Cell(1, 1).GetString().Should().Be(StudentWorkbookStore.RollStateColumn);
            loaded.RollStateJson.Should().NotBeNullOrWhiteSpace();

            var backups = Directory.GetFiles(Path.GetDirectoryName(tempPath)!, backupPattern);
            backups.Should().ContainSingle();
            File.ReadAllBytes(backups[0]).Should().Equal(originalBytes);
            using var backupWorkbook = new XLWorkbook(backups[0]);
            backupWorkbook.Worksheet("1班").Cell(5, 8).FormulaA1.Should().Be("1+1");
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
            foreach (var backup in Directory.GetFiles(Path.GetDirectoryName(tempPath)!, backupPattern))
            {
                File.Delete(backup);
            }
        }
    }

}
