namespace ClassroomToolkit.App.Paint;

internal enum PhotoNavigationInkViewportSyncAction
{
    None = 0,
    UpdatePanCompensation = 1,
    ResetPanCompensation = 2
}

internal static class PhotoNavigationInkViewportSyncPolicy
{
    internal static PhotoNavigationInkViewportSyncAction ResolveAction(
        bool photoInkModeActive,
        bool interactiveSwitch)
    {
        if (!photoInkModeActive)
        {
            return PhotoNavigationInkViewportSyncAction.None;
        }

        return interactiveSwitch
            ? PhotoNavigationInkViewportSyncAction.UpdatePanCompensation
            : PhotoNavigationInkViewportSyncAction.ResetPanCompensation;
    }
}
