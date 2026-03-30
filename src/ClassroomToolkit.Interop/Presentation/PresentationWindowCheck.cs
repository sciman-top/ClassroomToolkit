namespace ClassroomToolkit.Interop.Presentation;

public sealed record PresentationWindowCheck(
    PresentationType Type,
    string ProcessName,
    IReadOnlyList<string> ClassNames,
    bool ClassMatch,
    bool ProcessMatch,
    bool HasCaption,
    bool IsFullscreen,
    int Score);
