using ClassroomToolkit.Domain.Models;

namespace ClassroomToolkit.Application.Abstractions;

public interface IStudentRepository
{
    StudentWorkbook LoadOrCreate(string path);
    void Save(StudentWorkbook workbook, string path, string? rollStateJson);
}
