namespace ClassroomToolkit.Infra.Storage;

public sealed record InkHistoryLoadResult(
    string SourcePath,
    int PageIndex,
    string? StrokesJson,
    bool CreatedTemplate);
