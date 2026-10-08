using System.Text.Json;
using ClosedXML.Excel;
using ClassroomToolkit.Domain.Models;
using ClassroomToolkit.Domain.Serialization;
using ClassroomToolkit.Domain.Utilities;

namespace ClassroomToolkit.Infra.Storage;

internal sealed record StudentWorkbookSpreadsheetReadResult(
    StudentWorkbook Workbook,
    string? RollStateJson,
    bool NeedsRepair);

internal static class StudentWorkbookSpreadsheetCodec
{
    private static readonly string[] DefaultHeaders = { "学号", "姓名", "分组" };
    private static readonly string[] CanonicalColumns = { "学号", "姓名", "分组", ClassRoster.InternalRowIdColumn };
    private const string InternalRowIdColumn = ClassRoster.InternalRowIdColumn;

    /// <summary>表头识别扫描行数：兼容真实表头之上有装饰性标题行的工作簿。</summary>
    private const int HeaderScanRowCount = 5;

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


    internal static StudentWorkbookSpreadsheetReadResult Read(XLWorkbook workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        var rollStateJson = ExtractRollState(workbook, out var rollStateNeedsRepair);
        var classes = new Dictionary<string, ClassRoster>(StringComparer.OrdinalIgnoreCase);
        var mergedDuplicateClassSheets = false;

        foreach (var sheet in workbook.Worksheets)
        {
            if (sheet.Name.Equals(StudentWorkbookStore.RollStateSheetName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var roster = ReadWorksheet(sheet);
            if (classes.TryGetValue(roster.ClassName, out var existing))
            {
                classes[roster.ClassName] = MergeClassRosters(existing, roster);
                mergedDuplicateClassSheets = true;
            }
            else
            {
                classes[roster.ClassName] = roster;
            }
        }

        var normalizedWorkbook = NormalizeWorkbook(classes, out var workbookNeedsRepair);
        var normalizedRollStateJson = EnsureRollStateJson(rollStateJson);
        var needsRepair = rollStateNeedsRepair
            || workbookNeedsRepair
            || mergedDuplicateClassSheets
            || !string.Equals(rollStateJson, normalizedRollStateJson, StringComparison.Ordinal);
        return new StudentWorkbookSpreadsheetReadResult(normalizedWorkbook, normalizedRollStateJson, needsRepair);
    }

    internal static void Write(XLWorkbook workbookDocument, StudentWorkbook workbook, string? rollStateJson)
    {
        ArgumentNullException.ThrowIfNull(workbookDocument);
        ArgumentNullException.ThrowIfNull(workbook);

        foreach (var pair in workbook.Classes)
        {
            var sheet = workbookDocument.Worksheets.Add(pair.Key);
            WriteWorksheet(sheet, pair.Value);
        }

        var stateSheet = workbookDocument.Worksheets.Add(StudentWorkbookStore.RollStateSheetName);
        stateSheet.Cell(1, 1).Value = StudentWorkbookStore.RollStateColumn;
        stateSheet.Cell(2, 1).Value = EnsureRollStateJson(rollStateJson);
        stateSheet.Column(1).Width = 100;
    }

    internal static StudentWorkbookLoadResult CreateTemplateWorkbook()
    {
        var students = new List<StudentRecord>
        {
            StudentRecord.Create("01", "张三", StudentWorkbookStore.DefaultClassName, "A"),
            StudentRecord.Create("02", "李四", StudentWorkbookStore.DefaultClassName, "B"),
            StudentRecord.Create("03", "王五", StudentWorkbookStore.DefaultClassName, "C"),
        };
        var roster = new ClassRoster(StudentWorkbookStore.DefaultClassName, students, CanonicalColumns);
        var workbook = new StudentWorkbook(new Dictionary<string, ClassRoster> { [StudentWorkbookStore.DefaultClassName] = roster }, StudentWorkbookStore.DefaultClassName);
        var rollStateJson = EnsureRollStateJson(null);
        return new StudentWorkbookLoadResult(workbook, false, rollStateJson);
    }

    private static string? ExtractRollState(XLWorkbook workbook, out bool needsRepair)
    {
        needsRepair = false;
        if (!workbook.TryGetWorksheet(StudentWorkbookStore.RollStateSheetName, out var sheet))
        {
            needsRepair = true;
            return null;
        }
        var header = sheet.Cell(1, 1).GetString().Trim();
        if (!header.Equals(StudentWorkbookStore.RollStateColumn, StringComparison.OrdinalIgnoreCase))
        {
            needsRepair = true;
        }
        var value = sheet.Cell(2, 1).GetString().Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static ClassRoster ReadWorksheet(IXLWorksheet sheet)
    {
        var rowsUsed = sheet.RowsUsed().ToList();
        if (rowsUsed.Count == 0)
        {
            return new ClassRoster(sheet.Name, Array.Empty<StudentRecord>());
        }

        // 前 HeaderScanRowCount 行内寻找同时含“学号”与“姓名”（含别名）的表头行，
        // 兼容真实表头之上有装饰性标题行的工作簿。找不到视为无法识别的表头：
        // 必须按读取失败降级（经 LoadOrCreate 异常通道进入只读+拒绝覆盖），
        // 绝不能把解析不出学生的空名单当自愈结果回写、覆盖用户原始数据。
        List<string> columnOrder = new();
        Dictionary<string, List<int>> headerMap = new(StringComparer.OrdinalIgnoreCase);
        var headerRowNumber = 0;
        var headerRecognized = false;
        foreach (var row in rowsUsed.Take(HeaderScanRowCount))
        {
            headerRowNumber = row.RowNumber();
            if (TryBuildHeaderColumns(row, out columnOrder, out headerMap))
            {
                headerRecognized = true;
                break;
            }
        }
        if (!headerRecognized)
        {
            throw new InvalidDataException(
                $"工作表“{sheet.Name}”前 {HeaderScanRowCount} 行内未找到包含“学号”和“姓名”列的表头，无法识别学生名单。");
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
        foreach (var row in rowsUsed.Where(r => r.RowNumber() > headerRowNumber))
        {
            var rowCache = new Dictionary<int, string>();
            var studentId = GetCellValue(row, headerMap, "学号", rowCache);
            var name = GetCellValue(row, headerMap, "姓名", rowCache);
            if (string.IsNullOrWhiteSpace(studentId) && string.IsNullOrWhiteSpace(name))
            {
                // 两列皆空的行不是学生数据，跳过；半录入行（只缺学号或只缺姓名）必须继续传递：
                // NormalizeRoster 会剔除并置 repaired，触发规范化备份，
                // 避免点名保存整册回写时把老师未录完的行无备份地永久删除。
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

    private static bool TryBuildHeaderColumns(
        IXLRow row,
        out List<string> columnOrder,
        out Dictionary<string, List<int>> headerMap)
    {
        columnOrder = new List<string>();
        headerMap = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in row.CellsUsed())
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

        // 同时识别出学号与姓名两列才认定是表头行；否则行内容无法解析成学生记录。
        return headerMap.ContainsKey("学号") && headerMap.ContainsKey("姓名");
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
            throw new InvalidDataException("学生工作簿没有班级工作表，已阻止用示例名单覆盖原文件。");
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
                // 班级语义由工作表名承载：ClassRoster 会给缺少“班级”表头的工作簿补虚拟班级列
                // （DefaultColumns），不能据此判修复，否则每次加载都会永远触发规范化回写。
                // 真实“班级”列的值若与表名不符，由逐行 student.ClassName 检查兜底置 repaired。
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

        // 班级列（真实或虚拟补齐）从不写回工作簿，列序比对必须在两侧同时剔除。
        var sourceColumns = new List<string>();
        foreach (var column in roster.ColumnOrder)
        {
            if (column.Equals("班级", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            sourceColumns.Add(column);
        }

        if (!SequenceEqualIgnoreCase(sourceColumns, columns))
        {
            repaired = true;
        }

        return new ClassRoster(className, students, columns);
    }

    private static ClassRoster MergeClassRosters(ClassRoster first, ClassRoster second)
    {
        var columns = first.ColumnOrder.ToList();
        foreach (var column in second.ColumnOrder)
        {
            if (!columns.Contains(column, StringComparer.OrdinalIgnoreCase))
            {
                columns.Add(column);
            }
        }

        var students = new List<StudentRecord>(first.Students.Count + second.Students.Count);
        students.AddRange(first.Students);
        students.AddRange(second.Students);
        return new ClassRoster(first.ClassName, students, columns);
    }

    private static string NormalizeClassName(string className)
    {
        var normalized = IdentityUtils.NormalizeText(className);
        return string.IsNullOrWhiteSpace(normalized) ? StudentWorkbookStore.DefaultClassName : normalized;
    }

    private static bool SequenceEqualIgnoreCase(
        List<string> source,
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
        if (!string.IsNullOrWhiteSpace(rollStateJson) && IsValidRollStateJson(rollStateJson))
        {
            return rollStateJson;
        }

        return RollStateSerializer.SerializeWorkbookStates(
            new Dictionary<string, ClassRollState>(StringComparer.OrdinalIgnoreCase));
    }

    private static bool IsValidRollStateJson(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (root.TryGetProperty("states", out var statesNode))
            {
                return AreValidClassStateEntries(statesNode);
            }

            foreach (var property in root.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.Object
                    || RollStateSerializer.DeserializeClassState(property.Value.GetRawText()) == null)
                {
                    return false;
                }
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    private static bool AreValidClassStateEntries(JsonElement statesNode)
    {
        if (statesNode.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var property in statesNode.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Object
                || RollStateSerializer.DeserializeClassState(property.Value.GetRawText()) == null)
            {
                return false;
            }
        }

        return true;
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
