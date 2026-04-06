namespace ClassroomToolkit.Application.Abstractions;

public sealed record InkHistorySnapshotLoadResult(
    string SourcePath,
    int PageIndex,
    string? StrokesJson,
    bool CreatedTemplate);

public interface IInkHistorySnapshotStore
{
    InkHistorySnapshotLoadResult LoadOrCreate(string sourcePath, int pageIndex, bool writeSnapshot = true);
    void Save(string sourcePath, int pageIndex, string? strokesJson);
}
