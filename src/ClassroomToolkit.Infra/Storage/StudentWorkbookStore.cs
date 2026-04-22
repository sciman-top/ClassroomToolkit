using ClosedXML.Excel;
using ClassroomToolkit.Domain.Models;
using ClassroomToolkit.Domain.Serialization;
using ClassroomToolkit.Domain.Utilities;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace ClassroomToolkit.Infra.Storage;

public sealed record StudentWorkbookLoadResult(StudentWorkbook Workbook, bool CreatedTemplate, string? RollStateJson);

public sealed class StudentWorkbookStore
{
    public const string DefaultClassName = "1班";
    public const string RollStateSheetName = "_ROLL_STATE";
    public const string RollStateColumn = "ROLL_STATE_JSON";

    private static readonly string[] DefaultHeaders = { "学号", "姓名", "分组" };
    private static readonly string[] CanonicalColumns = { "学号", "姓名", "分组", ClassRoster.InternalRowIdColumn };
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
            TrySaveWorkbook(template.Workbook, path, template.RollStateJson);
            return template with { CreatedTemplate = true };
        }

        try
        {
            using var workbook = new XLWorkbook(path);
            var rollStateJson = ExtractRollState(workbook, out var rollStateNeedsRepair);
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

            var normalizedWorkbook = NormalizeWorkbook(classes, out var workbookNeedsRepair);
            var normalizedRollStateJson = EnsureRollStateJson(rollStateJson);
            if (rollStateNeedsRepair || workbookNeedsRepair || !string.Equals(rollStateJson, normalizedRollStateJson, StringComparison.Ordinal))
            {
                TrySaveWorkbook(normalizedWorkbook, path, normalizedRollStateJson);
            }

            return new StudentWorkbookLoadResult(normalizedWorkbook, false, normalizedRollStateJson);
        }
        catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex) && ShouldFallbackToTemplateOnReadFailure(ex))
        {
            var template = CreateTemplateWorkbook();
            TrySaveWorkbook(template.Workbook, path, template.RollStateJson);
            return template with { CreatedTemplate = false };
        }
    }

    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Public instance API is intentionally preserved for compatibility with existing consumers.")]
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
                var stateSheet = xl.Worksheets.Add(RollStateSheetName);
                stateSheet.Cell(1, 1).Value = RollStateColumn;
                stateSheet.Cell(2, 1).Value = EnsureRollStateJson(rollStateJson);
                stateSheet.Column(1).Width = 100;
                xl.SaveAs(tempPath);
            }
            if (File.Exists(path))
            {
                AtomicFileReplaceUtility.ReplaceOrOverwrite(tempPath, path);
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
                    Debug.WriteLine(
                        $"[StudentWorkbookStore] temp cleanup failed path={tempPath} ex={ex.GetType().Name} msg={ex.Message}");
                }
            }
        }
    }

    private void TrySaveWorkbook(StudentWorkbook workbook, string path, string? rollStateJson)
    {
        try
        {
            Save(workbook, path, rollStateJson);
        }
        catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine(
                $"[StudentWorkbookStore] self-heal save failed path={path} ex={ex.GetType().Name} msg={ex.Message}");
        }
    }

    private static bool ShouldFallbackToTemplateOnReadFailure(Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);

        // Access/IO failures are operational issues and must surface to caller.
        // Only parsing/format-style failures should trigger self-heal fallback.
        return ex is not (
            IOException
            or UnauthorizedAccessException
            or PathTooLongException
            or NotSupportedException);
    }

    private static StudentWorkbookLoadResult CreateTemplateWorkbook()
    {
        var students = new List<StudentRecord>
        {
            StudentRecord.Create("01", "张三", DefaultClassName, "A"),
            StudentRecord.Create("02", "李四", DefaultClassName, "B"),
            StudentRecord.Create("03", "王五", DefaultClassName, "C"),
        };
        var roster = new ClassRoster(DefaultClassName, students, CanonicalColumns);
        var workbook = new StudentWorkbook(new Dictionary<string, ClassRoster> { [DefaultClassName] = roster }, DefaultClassName);
        var rollStateJson = EnsureRollStateJson(null);
        return new StudentWorkbookLoadResult(workbook, false, rollStateJson);
    }

    private static string? ExtractRollState(XLWorkbook workbook, out bool needsRepair)
    {
        needsRepair = false;
        if (!workbook.TryGetWorksheet(RollStateSheetName, out var sheet))
        {
            needsRepair = true;
            return null;
        }
        var header = sheet.Cell(1, 1).GetString().Trim();
        if (!header.Equals(RollStateColumn, StringComparison.OrdinalIgnoreCase))
        {
            needsRepair = true;
        }
        var value = sheet.Cell(2, 1).GetString().Trim();
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
                if (column.Equals("班级", StringComparison.OrdinalIgnoreCase))
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
        ApplyColumnWidths(sheet, columns);
    }

    private static bool IsDefaultColumn(string column)
    {
        return DefaultHeaders.Contains(column, StringComparer.OrdinalIgnoreCase);
    }

    private static List<string> BuildWriteColumns(ClassRoster roster)
    {
        var columns = CanonicalColumns.ToList();
        foreach (var column in roster.ColumnOrder)
        {
            if (IsCanonicalColumn(column) || column.Equals("班级", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            if (!columns.Contains(column, StringComparer.OrdinalIgnoreCase))
            {
                columns.Add(column);
            }
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

    private static StudentWorkbook NormalizeWorkbook(
        Dictionary<string, ClassRoster> classes,
        out bool needsRepair)
    {
        needsRepair = false;
        var normalized = new Dictionary<string, ClassRoster>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in classes)
        {
            var className = NormalizeClassName(pair.Key);
            if (string.IsNullOrWhiteSpace(className))
            {
                needsRepair = true;
                continue;
            }

            var roster = NormalizeRoster(className, pair.Value, out var rosterRepaired);
            needsRepair |= rosterRepaired;
            if (!string.Equals(className, pair.Key, StringComparison.Ordinal))
            {
                needsRepair = true;
            }

            normalized[className] = roster;
        }

        if (normalized.Count == 0)
        {
            needsRepair = true;
            return CreateTemplateWorkbook().Workbook;
        }

        return new StudentWorkbook(normalized, normalized.Keys.FirstOrDefault());
    }

    private static ClassRoster NormalizeRoster(
        string className,
        ClassRoster roster,
        out bool repaired)
    {
        repaired = false;
        var students = new List<StudentRecord>();
        var discoveredColumns = new List<string>();

        foreach (var column in roster.ColumnOrder)
        {
            var normalizedColumn = IdentityUtils.NormalizeText(column);
            if (string.IsNullOrWhiteSpace(normalizedColumn))
            {
                repaired = true;
                continue;
            }
            if (normalizedColumn.Equals("班级", StringComparison.OrdinalIgnoreCase))
            {
                repaired = true;
                continue;
            }
            if (!IsCanonicalColumn(normalizedColumn) && !discoveredColumns.Contains(normalizedColumn, StringComparer.OrdinalIgnoreCase))
            {
                discoveredColumns.Add(normalizedColumn);
            }
        }

        foreach (var student in roster.Students)
        {
            var studentId = IdentityUtils.CompactText(student.StudentId);
            var name = IdentityUtils.NormalizeText(student.Name);
            if (string.IsNullOrWhiteSpace(studentId) || string.IsNullOrWhiteSpace(name))
            {
                repaired = true;
                continue;
            }

            var groupName = IdentityUtils.NormalizeGroupName(student.GroupName);
            var rowId = string.IsNullOrWhiteSpace(student.RowId)
                ? Guid.NewGuid().ToString("N")
                : student.RowId.Trim();
            if (!string.Equals(rowId, student.RowId, StringComparison.Ordinal)
                || !string.Equals(student.ClassName, className, StringComparison.Ordinal)
                || !string.Equals(studentId, student.StudentId, StringComparison.Ordinal)
                || !string.Equals(name, student.Name, StringComparison.Ordinal)
                || !string.Equals(groupName, student.GroupName, StringComparison.Ordinal))
            {
                repaired = true;
            }

            var extras = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in student.ExtraFields)
            {
                var extraKey = IdentityUtils.NormalizeText(pair.Key);
                if (string.IsNullOrWhiteSpace(extraKey)
                    || IsCanonicalColumn(extraKey)
                    || extraKey.Equals("班级", StringComparison.OrdinalIgnoreCase))
                {
                    repaired = true;
                    continue;
                }
                var extraValue = IdentityUtils.NormalizeText(pair.Value);
                if (string.IsNullOrWhiteSpace(extraValue))
                {
                    continue;
                }
                extras[extraKey] = extraValue;
                if (!discoveredColumns.Contains(extraKey, StringComparer.OrdinalIgnoreCase))
                {
                    discoveredColumns.Add(extraKey);
                }
            }

            students.Add(StudentRecord.Create(studentId, name, className, groupName, rowId, extras));
        }

        var columns = CanonicalColumns.ToList();
        foreach (var extra in discoveredColumns)
        {
            if (!columns.Contains(extra, StringComparer.OrdinalIgnoreCase))
            {
                columns.Add(extra);
            }
        }

        if (!SequenceEqualIgnoreCase(roster.ColumnOrder, columns))
        {
            repaired = true;
        }

        return new ClassRoster(className, students, columns);
    }

    private static string NormalizeClassName(string className)
    {
        var normalized = IdentityUtils.NormalizeText(className);
        return string.IsNullOrWhiteSpace(normalized) ? DefaultClassName : normalized;
    }

    private static bool SequenceEqualIgnoreCase(
        IReadOnlyList<string> source,
        List<string> target)
    {
        if (source.Count != target.Count)
        {
            return false;
        }

        for (var i = 0; i < source.Count; i++)
        {
            if (!string.Equals(source[i], target[i], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsCanonicalColumn(string column)
    {
        return CanonicalColumns.Contains(column, StringComparer.OrdinalIgnoreCase);
    }

    private static string EnsureRollStateJson(string? rollStateJson)
    {
        if (!string.IsNullOrWhiteSpace(rollStateJson))
        {
            return rollStateJson;
        }

        return RollStateSerializer.SerializeWorkbookStates(
            new Dictionary<string, ClassRollState>(StringComparer.OrdinalIgnoreCase));
    }

    private static void ApplyColumnWidths(IXLWorksheet sheet, List<string> columns)
    {
        for (var i = 0; i < columns.Count; i++)
        {
            var column = columns[i];
            var width = column switch
            {
                "学号" => 12,
                "姓名" => 16,
                "分组" => 12,
                InternalRowIdColumn => 38,
                _ => 20
            };
            sheet.Column(i + 1).Width = width;
        }
    }
}
