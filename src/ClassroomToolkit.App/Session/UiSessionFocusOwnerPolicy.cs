namespace ClassroomToolkit.App.Session;

internal static class UiSessionFocusOwnerPolicy
{
    public static UiFocusOwner Resolve(UiSceneKind scene)
    {
        return scene switch
        {
            UiSceneKind.PresentationFullscreen => UiFocusOwner.Presentation,
            UiSceneKind.PhotoFullscreen => UiFocusOwner.Photo,
            UiSceneKind.Whiteboard => UiFocusOwner.Whiteboard,
            _ => UiFocusOwner.None
        };
    }
}
