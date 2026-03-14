using ClassroomToolkit.Domain.Models;

namespace ClassroomToolkit.Infra.Storage;

public interface IStudentWorkbookStoreBridge
{
    StudentWorkbookLoadResult LoadOrCreate(string path);
    void Save(StudentWorkbook workbook, string path, string? rollStateJson);
}

public sealed class StudentWorkbookStoreBridge : IStudentWorkbookStoreBridge
{
    private readonly StudentWorkbookStore _store = new();

    public StudentWorkbookLoadResult LoadOrCreate(string path)
    {
        return _store.LoadOrCreate(path);
    }

    public void Save(StudentWorkbook workbook, string path, string? rollStateJson)
    {
        _store.Save(workbook, path, rollStateJson);
    }
}
