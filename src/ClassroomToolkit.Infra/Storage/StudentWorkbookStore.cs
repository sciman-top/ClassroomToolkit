using ClosedXML.Excel;
using ClassroomToolkit.Domain.Models;
using ClassroomToolkit.Domain.Utilities;

namespace ClassroomToolkit.Infra.Storage;

public sealed record StudentWorkbookLoadResult(StudentWorkbook Workbook, bool CreatedTemplate, string? RollStateJson);

public sealed class StudentWorkbookStore
{
    public const string RollStateSheetName = "_ROLL_STATE";
    public const string RollStateColumn = "ROLL_STATE_JSON";

    private static readonly string[] DefaultHeaders = { "学号", "姓名", "班级", "分组" };

    private static readonly Dictionary<string, string> HeaderAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["学号 "] = "学号",
        ["学生学号"] = "学号",
        ["学生编号"] = "学号",
        ["编号"] = "学号",
        ["姓名 "] = "姓名",
        ["名字"] = "姓名",
        ["学生姓名"] = "姓名",
        ["班级 "] = "班级",
        ["班别"] = "班级",
        ["班级名称"] = "班级",
        ["分组 "] = "分组",
        ["小组"] = "分组",
        ["组别"] = "分组",
        ["分组名称"] = "分组",
    };

    public StudentWorkbookLoadResult LoadOrCreate(string path)
    {
        if (!File.Exists(path))
        {
            var template = CreateTemplateWorkbook();
            Save(template.Workbook, path, template.RollStateJson);
            return template with { CreatedTemplate = true };
        }

        using var workbook = new XLWorkbook(path);
        var rollStateJson = ExtractRollState(workbook);
        var classes = new Dictionary<string, ClassRoster>(StringComparer.OrdinalIgnoreCase);

        foreach (var sheet in workbook.Worksheets)
        {
            if (sheet.Name.Equals(RollStateSheetName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            var roster = ReadWorksheet(sheet);
            classes[roster.ClassName] = roster;
        }

        var resultWorkbook = new StudentWorkbook(classes, classes.Keys.FirstOrDefault());
        return new StudentWorkbookLoadResult(resultWorkbook, false, rollStateJson);
    }

    public void Save(StudentWorkbook workbook, string path, string? rollStateJson)
    {
        using var xl = new XLWorkbook();
        foreach (var pair in workbook.Classes)
        {
            var sheet = xl.Worksheets.Add(pair.Key);
            WriteWorksheet(sheet, pair.Value);
        }
        if (!string.IsNullOrWhiteSpace(rollStateJson))
        {
            var stateSheet = xl.Worksheets.Add(RollStateSheetName);
            stateSheet.Cell(1, 1).Value = RollStateColumn;
            stateSheet.Cell(2, 1).Value = rollStateJson;
        }
        xl.SaveAs(path);
    }

    private static StudentWorkbookLoadResult CreateTemplateWorkbook()
    {
        var students = new List<StudentRecord>
        {
            StudentRecord.Create("101", "张三", "班级1", "一组"),
            StudentRecord.Create("102", "李四", "班级1", "二组"),
            StudentRecord.Create("103", "王五", "班级1", "一组"),
        };
        var roster = new ClassRoster("班级1", students);
        var workbook = new StudentWorkbook(new Dictionary<string, ClassRoster> { ["班级1"] = roster }, "班级1");
        return new StudentWorkbookLoadResult(workbook, false, null);
    }

    private static string? ExtractRollState(XLWorkbook workbook)
    {
        if (!workbook.TryGetWorksheet(RollStateSheetName, out var sheet))
        {
            return null;
        }
        var value = sheet.Cell(2, 1).GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static ClassRoster ReadWorksheet(IXLWorksheet sheet)
    {
        var headerRow = sheet.FirstRowUsed();
        if (headerRow == null)
        {
            return new ClassRoster(sheet.Name, Array.Empty<StudentRecord>());
        }
        var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in headerRow.CellsUsed())
        {
            var raw = cell.GetString().Trim();
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }
            if (HeaderAliases.TryGetValue(raw, out var canonical))
            {
                raw = canonical;
            }
            if (!headerMap.ContainsKey(raw))
            {
                headerMap[raw] = cell.Address.ColumnNumber;
            }
        }

        var students = new List<StudentRecord>();
        foreach (var row in sheet.RowsUsed().Where(r => r.RowNumber() > headerRow.RowNumber()))
        {
            var studentId = GetCellValue(row, headerMap, "学号");
            var name = GetCellValue(row, headerMap, "姓名");
            if (string.IsNullOrWhiteSpace(studentId) || string.IsNullOrWhiteSpace(name))
            {
                continue;
            }
            var className = GetCellValue(row, headerMap, "班级");
            if (string.IsNullOrWhiteSpace(className))
            {
                className = sheet.Name;
            }
            var groupName = GetCellValue(row, headerMap, "分组");
            var record = StudentRecord.Create(studentId, name, className, groupName);
            students.Add(record);
        }
        return new ClassRoster(sheet.Name, students);
    }

    private static string GetCellValue(IXLRow row, Dictionary<string, int> map, string key)
    {
        if (!map.TryGetValue(key, out var col))
        {
            return string.Empty;
        }
        return row.Cell(col).GetString().Trim();
    }

    private static void WriteWorksheet(IXLWorksheet sheet, ClassRoster roster)
    {
        for (var i = 0; i < DefaultHeaders.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = DefaultHeaders[i];
        }
        var rowIndex = 2;
        foreach (var student in roster.Students)
        {
            sheet.Cell(rowIndex, 1).Value = student.StudentId;
            sheet.Cell(rowIndex, 2).Value = student.Name;
            sheet.Cell(rowIndex, 3).Value = student.ClassName;
            sheet.Cell(rowIndex, 4).Value = student.GroupName;
            rowIndex++;
        }
        sheet.Columns().AdjustToContents();
    }
}
