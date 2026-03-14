using ClassroomToolkit.Application.Abstractions;
using ClassroomToolkit.Domain.Models;
using ClassroomToolkit.Domain.Serialization;

namespace ClassroomToolkit.Application.UseCases.RollCall;

public sealed record RollCallWorkbookLoadResult(
    StudentWorkbook Workbook,
    Dictionary<string, ClassRollState> ClassStates,
    string? ErrorMessage);

public sealed class RollCallWorkbookUseCase
{
    private readonly IRollCallWorkbookStore _store;

    public RollCallWorkbookUseCase(IRollCallWorkbookStore store)
    {
        _store = store;
    }

    public RollCallWorkbookLoadResult Load(string path)
    {
        try
        {
            var result = _store.LoadOrCreate(path);
            var states = new Dictionary<string, ClassRollState>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in RollStateSerializer.DeserializeWorkbookStates(result.RollStateJson))
            {
                states[pair.Key] = pair.Value;
            }

            return new RollCallWorkbookLoadResult(result.Workbook, states, null);
        }
        catch (Exception ex)
        {
            var fallbackRoster = new ClassRoster("班级1", Array.Empty<StudentRecord>());
            var fallbackWorkbook = new StudentWorkbook(new Dictionary<string, ClassRoster> { ["班级1"] = fallbackRoster }, "班级1");
            return new RollCallWorkbookLoadResult(
                fallbackWorkbook,
                new Dictionary<string, ClassRollState>(),
                $"学生名册读取失败：{ex.Message}");
        }
    }

    public void Save(string path, StudentWorkbook workbook, IReadOnlyDictionary<string, ClassRollState> classStates)
    {
        var payload = new Dictionary<string, ClassRollState>(classStates, StringComparer.OrdinalIgnoreCase);
        var rollStateJson = RollStateSerializer.SerializeWorkbookStates(payload);
        _store.Save(workbook, path, rollStateJson);
    }
}
