using ClassroomToolkit.Domain.Models;

namespace ClassroomToolkit.Application.Abstractions;

public sealed record InkHistorySnapshotLoadResult(
    string SourcePath,
    int PageIndex,
    string? StrokesJson,
    bool CreatedTemplate,
    DateTime? UpdatedAtUtc = null);

public interface IInkHistorySnapshotStore
{
    InkHistorySnapshotLoadResult LoadOrCreate(string sourcePath, int pageIndex, bool writeSnapshot = true);
    bool Save(string sourcePath, int pageIndex, string? strokesJson);
}

public interface IInkHistoryStoreBridge
{
    InkHistoryLoadResult LoadOrCreate(string sourcePath, int pageIndex);
    bool Save(string sourcePath, int pageIndex, string? strokesJson);
}

public sealed record InkHistoryLoadResult(
    string SourcePath,
    int PageIndex,
    string? StrokesJson,
    bool CreatedTemplate,
    DateTime? UpdatedAtUtc = null);

public interface ISettingsDocumentStore
{
    bool IsOverwriteBlocked => false;
    Dictionary<string, Dictionary<string, string>> Load();
    void Save(Dictionary<string, Dictionary<string, string>> data);
}

public sealed record RollCallWorkbookStoreLoadData(
    StudentWorkbook Workbook,
    bool CreatedTemplate,
    string? RollStateJson,
    // 加载时规范化备份写失败即进入只读降级：数据可用，但保存必须被抑制并提前告知。
    bool OverwriteBlocked = false);

public interface IRollCallWorkbookStore
{
    RollCallWorkbookStoreLoadData LoadOrCreate(string path);
    void Save(StudentWorkbook workbook, string path, string? rollStateJson);
}
