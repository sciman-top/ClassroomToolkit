namespace ClassroomToolkit.App.Session;

public static class UiSessionInkVisibilityPolicy
{
    public static UiInkVisibility Resolve(UiSceneKind scene, UiToolMode toolMode)
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
}
