using System;

namespace ClassroomToolkit.App.Session;

public static class UiSessionReducer
{
    public static UiSessionState Reduce(UiSessionState current, UiSessionEvent sessionEvent)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(sessionEvent);

        var next = sessionEvent switch
        {
            EnterPresentationFullscreenEvent => current with
            {
                Scene = UiSceneKind.PresentationFullscreen,
                FocusOwner = UiSessionFocusOwnerPolicy.Resolve(UiSceneKind.PresentationFullscreen)
            },
            ExitPresentationFullscreenEvent when current.Scene == UiSceneKind.PresentationFullscreen => current with
            {
                Scene = UiSceneKind.Idle,
                FocusOwner = UiSessionFocusOwnerPolicy.Resolve(UiSceneKind.Idle)
            },
            EnterPhotoFullscreenEvent => current with
            {
                Scene = UiSceneKind.PhotoFullscreen,
                FocusOwner = UiSessionFocusOwnerPolicy.Resolve(UiSceneKind.PhotoFullscreen)
            },
            ExitPhotoFullscreenEvent when current.Scene == UiSceneKind.PhotoFullscreen => current with
            {
                Scene = UiSceneKind.Idle,
                FocusOwner = UiSessionFocusOwnerPolicy.Resolve(UiSceneKind.Idle)
            },
            EnterWhiteboardEvent => current with
            {
                Scene = UiSceneKind.Whiteboard,
                FocusOwner = UiSessionFocusOwnerPolicy.Resolve(UiSceneKind.Whiteboard)
            },
            ExitWhiteboardEvent whiteboardExit when current.Scene == UiSceneKind.Whiteboard =>
                ReduceWhiteboardExit(current, whiteboardExit),
            SwitchToolModeEvent toolModeEvent => current with
            {
                ToolMode = toolModeEvent.ToolMode
            },
            MarkInkDirtyEvent => current with
            {
                InkDirty = true
            },
            MarkInkSavedEvent => current with
            {
                InkDirty = false
            },
            _ => current
        };

        return ApplyDerivedState(next);
    }

    private static UiSessionState ApplyDerivedState(UiSessionState state)
    {
        var navigationMode = UiSessionNavigationPolicy.Resolve(state.Scene, state.ToolMode);
        var inkVisibility = UiSessionInkVisibilityPolicy.Resolve(state.Scene, state.ToolMode);
        var overlayRequired = UiSessionOverlayVisibilityPolicy.IsOverlayTopmostRequired(state.Scene);
        var overlayVisible = UiSessionOverlayVisibilityPolicy.AreFloatingWidgetsVisible(state.Scene);

        return state with
        {
            NavigationMode = navigationMode,
            InkVisibility = inkVisibility,
            OverlayTopmostRequired = overlayRequired,
            RollCallVisible = overlayVisible,
            LauncherVisible = overlayVisible,
            ToolbarVisible = overlayVisible
        };
    }

    private static UiSessionState ReduceWhiteboardExit(UiSessionState current, ExitWhiteboardEvent whiteboardExit)
    {
        return whiteboardExit.ResumeScene switch
        {
            UiSceneKind.PhotoFullscreen => current with
            {
                Scene = UiSceneKind.PhotoFullscreen,
                FocusOwner = UiSessionFocusOwnerPolicy.Resolve(UiSceneKind.PhotoFullscreen)
            },
            UiSceneKind.PresentationFullscreen => current with
            {
                Scene = UiSceneKind.PresentationFullscreen,
                FocusOwner = UiSessionFocusOwnerPolicy.Resolve(UiSceneKind.PresentationFullscreen)
            },
            _ => current with
            {
                Scene = UiSceneKind.Idle,
                FocusOwner = UiSessionFocusOwnerPolicy.Resolve(UiSceneKind.Idle)
            }
        };
    }
}
