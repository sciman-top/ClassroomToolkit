namespace ClassroomToolkit.App.Models;

public sealed record StudentListItem(string StudentId, string StudentName, int Index, bool Called)
{
    public string DisplayText
    {
        get
        {
            var id = string.IsNullOrWhiteSpace(StudentId) ? "无学号" : StudentId;
            var name = string.IsNullOrWhiteSpace(StudentName) ? "未命名" : StudentName;
            return $"{id} {name}".Trim();
        }
    }
}
