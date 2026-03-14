using ClassroomToolkit.Domain.Models;

namespace ClassroomToolkit.Application.Abstractions;

public sealed record RollCallWorkbookStoreLoadData(
    StudentWorkbook Workbook,
    bool CreatedTemplate,
    string? RollStateJson);

public interface IRollCallWorkbookStore
{
    RollCallWorkbookStoreLoadData LoadOrCreate(string path);
    void Save(StudentWorkbook workbook, string path, string? rollStateJson);
}
