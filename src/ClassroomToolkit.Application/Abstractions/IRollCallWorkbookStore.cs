using ClassroomToolkit.Domain.Models;

namespace ClassroomToolkit.Application.Abstractions;

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
