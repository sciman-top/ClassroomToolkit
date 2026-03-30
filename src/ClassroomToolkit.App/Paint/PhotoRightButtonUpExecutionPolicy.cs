namespace ClassroomToolkit.App.Paint;

internal enum PhotoRightButtonUpAction
{
    PassThrough,
    ShowContextMenu
}

internal readonly record struct PhotoRightButtonUpExecutionPlan(
    PhotoRightButtonUpAction Action,
    bool ShouldMarkHandled,
    bool ShouldClearPending);

internal static class PhotoRightButtonUpExecutionPolicy
{
    internal static PhotoRightButtonUpExecutionPlan Resolve(bool shouldShowContextMenuOnUp)
    {
        if (!shouldShowContextMenuOnUp)
        {
            return new PhotoRightButtonUpExecutionPlan(
                Action: PhotoRightButtonUpAction.PassThrough,
                ShouldMarkHandled: false,
                ShouldClearPending: false);
        }

        return new PhotoRightButtonUpExecutionPlan(
            Action: PhotoRightButtonUpAction.ShowContextMenu,
            ShouldMarkHandled: true,
            ShouldClearPending: true);
    }
}
