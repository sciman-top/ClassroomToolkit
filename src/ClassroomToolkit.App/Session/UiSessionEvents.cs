namespace ClassroomToolkit.App.Session;

public abstract record UiSessionEvent;

internal sealed record EnterPresentationFullscreenEvent(PresentationSourceKind Source) : UiSessionEvent;

internal sealed record ExitPresentationFullscreenEvent : UiSessionEvent;

internal sealed record EnterPhotoFullscreenEvent(PhotoSourceKind Source) : UiSessionEvent;

internal sealed record ExitPhotoFullscreenEvent : UiSessionEvent;

internal sealed record EnterWhiteboardEvent : UiSessionEvent;

internal sealed record ExitWhiteboardEvent(
    UiSceneKind ResumeScene = UiSceneKind.Idle,
    PhotoSourceKind PhotoSource = PhotoSourceKind.Unknown,
    PresentationSourceKind PresentationSource = PresentationSourceKind.Unknown) : UiSessionEvent;

internal sealed record SwitchToolModeEvent(UiToolMode ToolMode) : UiSessionEvent;

internal sealed record MarkInkDirtyEvent : UiSessionEvent;

internal sealed record MarkInkSavedEvent : UiSessionEvent;
