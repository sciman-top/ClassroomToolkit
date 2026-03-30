namespace ClassroomToolkit.App.Session;

public abstract record UiSessionEvent;

public sealed record EnterPresentationFullscreenEvent(PresentationSourceKind Source) : UiSessionEvent;

public sealed record ExitPresentationFullscreenEvent : UiSessionEvent;

public sealed record EnterPhotoFullscreenEvent(PhotoSourceKind Source) : UiSessionEvent;

public sealed record ExitPhotoFullscreenEvent : UiSessionEvent;

public sealed record EnterWhiteboardEvent : UiSessionEvent;

public sealed record ExitWhiteboardEvent(
    UiSceneKind ResumeScene = UiSceneKind.Idle,
    PhotoSourceKind PhotoSource = PhotoSourceKind.Unknown,
    PresentationSourceKind PresentationSource = PresentationSourceKind.Unknown) : UiSessionEvent;

public sealed record SwitchToolModeEvent(UiToolMode ToolMode) : UiSessionEvent;

public sealed record MarkInkDirtyEvent : UiSessionEvent;

public sealed record MarkInkSavedEvent : UiSessionEvent;
