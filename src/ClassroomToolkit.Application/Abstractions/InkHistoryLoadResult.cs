namespace ClassroomToolkit.Application.Abstractions;

public sealed record InkHistoryLoadResult(
    string SourcePath,
    int PageIndex,
    string? StrokesJson,
    bool CreatedTemplate);
