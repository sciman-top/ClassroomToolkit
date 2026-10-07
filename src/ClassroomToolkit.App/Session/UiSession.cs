using ClassroomToolkit.App.Windowing;
using System;

namespace ClassroomToolkit.App.Session;

internal interface IUiSessionEffectRunner
{
    void Run(UiSessionTransition transition);
}

internal sealed record UiSessionWidgetVisibility(
    bool RollCallVisible,
    bool LauncherVisible,
    bool ToolbarVisible);

public sealed record UiSessionTransition(
    long Id,
    DateTime OccurredAtUtc,
    UiSessionEvent Event,
    UiSessionState Previous,
    UiSessionState Current)
{
    public bool HasStateChange => !Equals(Previous, Current);
}

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

internal static class UiSessionPolicies
{
    public static bool IsOverlayTopmostRequired(UiSceneKind scene) => scene != UiSceneKind.Idle;

    public static bool AreFloatingWidgetsVisible(UiSceneKind scene) => scene != UiSceneKind.Idle;

    public static bool AllowsPresentationInput(UiNavigationMode navigationMode)
    {
        return navigationMode is UiNavigationMode.Hybrid or UiNavigationMode.HookOnly;
    }

    public static bool ShouldRequestFloatingZOrder(UiSessionWidgetVisibility visibility)
    {
        ArgumentNullException.ThrowIfNull(visibility);

        return visibility.RollCallVisible
            || visibility.LauncherVisible
            || visibility.ToolbarVisible;
    }

    public static UiFocusOwner ResolveFocusOwner(UiSceneKind scene)
    {
        return scene switch
        {
            UiSceneKind.PresentationFullscreen => UiFocusOwner.Presentation,
            UiSceneKind.PhotoFullscreen => UiFocusOwner.Photo,
            UiSceneKind.Whiteboard => UiFocusOwner.Whiteboard,
            _ => UiFocusOwner.None
        };
    }

    public static UiInkVisibility ResolveInkVisibility(UiSceneKind scene, UiToolMode toolMode)
    {
        if (toolMode == UiToolMode.Draw)
        {
            return UiInkVisibility.VisibleEditable;
        }

        return scene switch
        {
            UiSceneKind.Idle => UiInkVisibility.Hidden,
            _ => UiInkVisibility.VisibleReadOnly
        };
    }

    public static UiNavigationMode ResolveNavigation(UiSceneKind scene, UiToolMode toolMode)
    {
        if (toolMode == UiToolMode.Draw)
        {
            return scene switch
            {
                UiSceneKind.PresentationFullscreen => UiNavigationMode.HookOnly,
                _ => UiNavigationMode.Disabled
            };
        }

        return scene switch
        {
            UiSceneKind.PresentationFullscreen => UiNavigationMode.Hybrid,
            UiSceneKind.PhotoFullscreen => UiNavigationMode.MessageOnly,
            _ => UiNavigationMode.Disabled
        };
    }

    public static bool TryResolveForOverlayTopmost(
        bool topmostRequired,
        out FloatingZOrderRequest request)
    {
        if (!topmostRequired)
        {
            request = default;
            return false;
        }

        request = new FloatingZOrderRequest(ForceEnforceZOrder: true);
        return true;
    }

    public static bool TryResolveForWidgetVisibility(
        UiSessionWidgetVisibility visibility,
        out FloatingZOrderRequest request)
    {
        if (!UiSessionPolicies.ShouldRequestFloatingZOrder(visibility))
        {
            request = default;
            return false;
        }

        request = new FloatingZOrderRequest(ForceEnforceZOrder: true);
        return true;
    }
}
