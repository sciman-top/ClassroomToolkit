using ClosedXML.Excel;
using ClassroomToolkit.Domain.Models;
using ClassroomToolkit.Domain.Utilities;

namespace ClassroomToolkit.Infra.Storage;

public sealed record StudentWorkbookLoadResult(StudentWorkbook Workbook, bool CreatedTemplate, string? RollStateJson);

public sealed class StudentWorkbookStore
{
    public const string RollStateSheetName = "_ROLL_STATE";
    public const string RollStateColumn = "ROLL_STATE_JSON";

    private static readonly string[] DefaultHeaders = ClassRoster.DefaultColumns;
    private const string InternalRowIdColumn = ClassRoster.InternalRowIdColumn;

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
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            var template = CreateTemplateWorkbook();
            Save(template.Workbook, path, template.RollStateJson);
            return template with { CreatedTemplate = true };
        }

        try
        {
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
        catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
        {
            var template = CreateTemplateWorkbook();
            return template with { CreatedTemplate = false };
        }
    }

    public void Save(StudentWorkbook workbook, string path, string? rollStateJson)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var directory = System.IO.Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
        var fileName = System.IO.Path.GetFileNameWithoutExtension(path);
        var extension = System.IO.Path.GetExtension(path);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".xlsx";
        }
        var tempPath = System.IO.Path.Combine(
            directory ?? string.Empty,
            $"{fileName}.tmp.{Guid.NewGuid():N}{extension}");
        try
        {
            using (var xl = new XLWorkbook())
            {
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
                xl.SaveAs(tempPath);
            }
            if (File.Exists(path))
            {
                TryReplaceOrOverwrite(tempPath, path);
            }
            else
            {
                File.Move(tempPath, path);
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
                {
                    // Best-effort cleanup for temp workbook files.
                }
            }
        }
    }

    private static void TryReplaceOrOverwrite(string tempPath, string targetPath)
    {
        try
        {
            File.Replace(tempPath, targetPath, null);
        }
        catch (Exception ex) when (AtomicReplaceFallbackPolicy.ShouldFallback(ex))
        {
            File.Copy(tempPath, targetPath, overwrite: true);
        }
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
        var columnOrder = new List<string>();
        var headerMap = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
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
            if (!columnOrder.Contains(raw, StringComparer.OrdinalIgnoreCase))
            {
                columnOrder.Add(raw);
            }
            if (!headerMap.TryGetValue(raw, out var list))
            {
                list = new List<int>();
                headerMap[raw] = list;
            }
            list.Add(cell.Address.ColumnNumber);
        }

        foreach (var column in DefaultHeaders)
        {
            if (!columnOrder.Contains(column, StringComparer.OrdinalIgnoreCase))
            {
                columnOrder.Add(column);
            }
        }
        if (!columnOrder.Contains(InternalRowIdColumn, StringComparer.OrdinalIgnoreCase))
        {
            columnOrder.Add(InternalRowIdColumn);
        }

        var students = new List<StudentRecord>();
        foreach (var row in sheet.RowsUsed().Where(r => r.RowNumber() > headerRow.RowNumber()))
        {
            var rowCache = new Dictionary<int, string>();
            var studentId = GetCellValue(row, headerMap, "学号", rowCache);
            var name = GetCellValue(row, headerMap, "姓名", rowCache);
            if (string.IsNullOrWhiteSpace(studentId) || string.IsNullOrWhiteSpace(name))
            {
                continue;
            }
            var className = GetCellValue(row, headerMap, "班级", rowCache);
            if (string.IsNullOrWhiteSpace(className))
            {
                className = sheet.Name;
            }
            var groupName = GetCellValue(row, headerMap, "分组", rowCache);
            var rowId = GetCellValue(row, headerMap, InternalRowIdColumn, rowCache);
            if (string.IsNullOrWhiteSpace(rowId))
            {
                rowId = Guid.NewGuid().ToString("N");
            }

            var extras = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var column in columnOrder)
            {
                if (IsDefaultColumn(column) || column.Equals(InternalRowIdColumn, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                var extraValue = GetCellValue(row, headerMap, column, rowCache);
                if (string.IsNullOrWhiteSpace(extraValue))
                {
                    continue;
                }
                extras[column] = IdentityUtils.NormalizeText(extraValue);
            }
            var record = StudentRecord.Create(studentId, name, className, groupName, rowId, extras);
            students.Add(record);
        }
        return new ClassRoster(sheet.Name, students, columnOrder);
    }

    private static string GetCellValue(
        IXLRow row,
        Dictionary<string, List<int>> map,
        string key,
        Dictionary<int, string>? cache = null)
    {
        if (!map.TryGetValue(key, out var cols))
        {
            return string.Empty;
        }
        foreach (var col in cols)
        {
            if (cache != null && cache.TryGetValue(col, out var cached))
            {
                if (!string.IsNullOrWhiteSpace(cached))
                {
                    return cached;
                }
                continue;
            }
            var value = row.Cell(col).GetString().Trim();
            cache?.TryAdd(col, value);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }
        return string.Empty;
    }

    private static void WriteWorksheet(IXLWorksheet sheet, ClassRoster roster)
    {
        var columns = BuildWriteColumns(roster);
        for (var i = 0; i < columns.Count; i++)
        {
            sheet.Cell(1, i + 1).Value = columns[i];
        }
        var rowIndex = 2;
        foreach (var student in roster.Students)
        {
            for (var i = 0; i < columns.Count; i++)
            {
                var column = columns[i];
                var cell = sheet.Cell(rowIndex, i + 1);
                if (column.Equals("学号", StringComparison.OrdinalIgnoreCase))
                {
                    cell.Value = student.StudentId;
                }
                else if (column.Equals("姓名", StringComparison.OrdinalIgnoreCase))
                {
                    cell.Value = student.Name;
                }
                else if (column.Equals("班级", StringComparison.OrdinalIgnoreCase))
                {
                    cell.Value = student.ClassName;
                }
                else if (column.Equals("分组", StringComparison.OrdinalIgnoreCase))
                {
                    cell.Value = student.GroupName;
                }
                else if (column.Equals(InternalRowIdColumn, StringComparison.OrdinalIgnoreCase))
                {
                    cell.Value = student.RowId;
                }
                else if (student.ExtraFields.TryGetValue(column, out var extra))
                {
                    cell.Value = extra;
                }
            }
            rowIndex++;
        }
        sheet.Columns().AdjustToContents();
    }

    private static bool IsDefaultColumn(string column)
    {
        return DefaultHeaders.Contains(column, StringComparer.OrdinalIgnoreCase);
    }

    private static List<string> BuildWriteColumns(ClassRoster roster)
    {
        var columns = roster.ColumnOrder?.ToList() ?? new List<string>();
        if (columns.Count == 0)
        {
            columns.AddRange(DefaultHeaders);
        }
        foreach (var column in DefaultHeaders)
        {
            if (!columns.Contains(column, StringComparer.OrdinalIgnoreCase))
            {
                columns.Add(column);
            }
        }
        if (!columns.Contains(InternalRowIdColumn, StringComparer.OrdinalIgnoreCase))
        {
            columns.Add(InternalRowIdColumn);
        }
        foreach (var student in roster.Students)
        {
            foreach (var extra in student.ExtraFields.Keys)
            {
                if (!columns.Contains(extra, StringComparer.OrdinalIgnoreCase))
                {
                    columns.Add(extra);
                }
            }
        }
        return columns;
    }
}
