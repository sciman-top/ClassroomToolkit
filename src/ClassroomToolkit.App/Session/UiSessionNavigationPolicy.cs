namespace ClassroomToolkit.App.Session;

public static class UiSessionNavigationPolicy
{
    public static UiNavigationMode Resolve(UiSceneKind scene, UiToolMode toolMode)
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
}
