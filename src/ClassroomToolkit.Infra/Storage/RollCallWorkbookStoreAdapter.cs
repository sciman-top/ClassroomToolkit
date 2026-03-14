using ClassroomToolkit.Application.Abstractions;

namespace ClassroomToolkit.Infra.Storage;

public sealed class RollCallWorkbookStoreAdapter : IRollCallWorkbookStore
{
    private readonly StudentWorkbookStore _store = new();

    public RollCallWorkbookStoreLoadData LoadOrCreate(string path)
    {
        var result = _store.LoadOrCreate(path);
        return new RollCallWorkbookStoreLoadData(result.Workbook, result.CreatedTemplate, result.RollStateJson);
    }

    public void Save(ClassroomToolkit.Domain.Models.StudentWorkbook workbook, string path, string? rollStateJson)
    {
        _store.Save(workbook, path, rollStateJson);
    }
}
