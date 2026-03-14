namespace ClassroomToolkit.App.Session;

public enum UiSceneKind
{
    Idle = 0,
    PresentationFullscreen,
    PhotoFullscreen,
    Whiteboard
}

public enum UiToolMode
{
    Cursor = 0,
    Draw
}

public enum UiNavigationMode
{
    Disabled = 0,
    MessageOnly,
    HookOnly,
    Hybrid
}

public enum UiFocusOwner
{
    None = 0,
    Presentation,
    Photo,
    Whiteboard,
    Overlay
}

public enum UiInkVisibility
{
    Hidden = 0,
    VisibleReadOnly,
    VisibleEditable
}

public enum PresentationSourceKind
{
    Unknown = 0,
    PowerPoint,
    Wps
}

public enum PhotoSourceKind
{
    Unknown = 0,
    Pdf,
    Image
}

public sealed record UiSessionState(
    UiSceneKind Scene,
    UiToolMode ToolMode,
    UiNavigationMode NavigationMode,
    UiFocusOwner FocusOwner,
    UiInkVisibility InkVisibility,
    bool InkDirty,
    bool OverlayTopmostRequired,
    bool RollCallVisible,
    bool LauncherVisible,
    bool ToolbarVisible)
{
    public static UiSessionState Default { get; } = new(
        Scene: UiSceneKind.Idle,
        ToolMode: UiToolMode.Draw,
        NavigationMode: UiNavigationMode.Disabled,
        FocusOwner: UiFocusOwner.None,
        InkVisibility: UiInkVisibility.VisibleEditable,
        InkDirty: false,
        OverlayTopmostRequired: false,
        RollCallVisible: false,
        LauncherVisible: true,
        ToolbarVisible: true);
}
