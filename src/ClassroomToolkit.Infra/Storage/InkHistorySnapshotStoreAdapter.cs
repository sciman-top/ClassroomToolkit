using ClassroomToolkit.Application.Abstractions;

namespace ClassroomToolkit.Infra.Storage;

public sealed class InkHistorySnapshotStoreAdapter : IInkHistorySnapshotStore
{
    private readonly InkHistorySqliteStoreAdapter _adapter;

    public InkHistorySnapshotStoreAdapter(InkHistorySqliteStoreAdapter adapter)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
    }

    public InkHistorySnapshotLoadResult LoadOrCreate(string sourcePath, int pageIndex, bool writeSnapshot = true)
    {
        var result = _adapter.LoadOrCreate(sourcePath, pageIndex, writeSnapshot);
        return new InkHistorySnapshotLoadResult(
            result.SourcePath,
            result.PageIndex,
            result.StrokesJson,
            result.CreatedTemplate);
    }

    public void Save(string sourcePath, int pageIndex, string? strokesJson)
    {
        _adapter.Save(sourcePath, pageIndex, strokesJson);
    }
}
