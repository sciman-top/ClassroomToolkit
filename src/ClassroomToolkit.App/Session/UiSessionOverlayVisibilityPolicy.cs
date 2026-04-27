namespace ClassroomToolkit.App.Session;

internal static class UiSessionOverlayVisibilityPolicy
{
    public static bool IsOverlayTopmostRequired(UiSceneKind scene) => scene != UiSceneKind.Idle;

    public static bool AreFloatingWidgetsVisible(UiSceneKind scene) => scene != UiSceneKind.Idle;
}
