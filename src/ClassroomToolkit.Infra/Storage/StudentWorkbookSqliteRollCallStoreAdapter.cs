using System;
using ClassroomToolkit.Application.Abstractions;

namespace ClassroomToolkit.Infra.Storage;

public sealed class StudentWorkbookSqliteRollCallStoreAdapter : IRollCallWorkbookStore
{
    private readonly StudentWorkbookSqliteStoreAdapter _adapter;

    public StudentWorkbookSqliteRollCallStoreAdapter()
        : this(new StudentWorkbookSqliteStoreAdapter())
    {
    }

    public StudentWorkbookSqliteRollCallStoreAdapter(StudentWorkbookSqliteStoreAdapter adapter)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
    }

    public RollCallWorkbookStoreLoadData LoadOrCreate(string path)
    {
        var result = _adapter.LoadOrCreate(path);
        return new RollCallWorkbookStoreLoadData(result.Workbook, result.CreatedTemplate, result.RollStateJson);
    }

    public void Save(Domain.Models.StudentWorkbook workbook, string path, string? rollStateJson)
    {
        _adapter.Save(workbook, path, rollStateJson);
    }
}
