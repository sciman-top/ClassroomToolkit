namespace ClassroomToolkit.Application.Abstractions;

public interface IInkHistoryStoreBridge
{
    InkHistoryLoadResult LoadOrCreate(string sourcePath, int pageIndex);
    void Save(string sourcePath, int pageIndex, string? strokesJson);
}
