using ClassroomToolkit.App.Session;

namespace ClassroomToolkit.App.Windowing;

internal static class UiSceneSurfaceMapper
{
    internal static ZOrderSurface Map(UiSceneKind scene)
    {
        return scene switch
        {
            UiSceneKind.PresentationFullscreen => ZOrderSurface.PresentationFullscreen,
            UiSceneKind.PhotoFullscreen => ZOrderSurface.PhotoFullscreen,
            UiSceneKind.Whiteboard => ZOrderSurface.Whiteboard,
            _ => ZOrderSurface.None
        };
    }
}
