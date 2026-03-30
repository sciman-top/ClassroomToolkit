namespace ClassroomToolkit.App.Paint;

internal enum PhotoPanMouseExecutionAction
{
    PassThrough,
    UpdatePan,
    EndPan
}

internal readonly record struct PhotoPanMouseExecutionPlan(
    PhotoPanMouseExecutionAction Action,
    bool ShouldMarkHandled);

internal static class PhotoPanMouseExecutionPolicy
{
    internal static PhotoPanMouseExecutionPlan ResolveMove(PhotoPanMouseMoveRoutingDecision decision)
    {
        return decision switch
        {
            PhotoPanMouseMoveRoutingDecision.UpdatePan => new PhotoPanMouseExecutionPlan(
                Action: PhotoPanMouseExecutionAction.UpdatePan,
                ShouldMarkHandled: true),
            PhotoPanMouseMoveRoutingDecision.EndPan => new PhotoPanMouseExecutionPlan(
                Action: PhotoPanMouseExecutionAction.EndPan,
                ShouldMarkHandled: true),
            _ => new PhotoPanMouseExecutionPlan(
                Action: PhotoPanMouseExecutionAction.PassThrough,
                ShouldMarkHandled: false)
        };
    }

    internal static PhotoPanMouseExecutionPlan ResolveEnd(
        bool isMousePhotoPanActive,
        bool shouldEndPan)
    {
        if (!isMousePhotoPanActive || !shouldEndPan)
        {
            return new PhotoPanMouseExecutionPlan(
                Action: PhotoPanMouseExecutionAction.PassThrough,
                ShouldMarkHandled: false);
        }

        return new PhotoPanMouseExecutionPlan(
            Action: PhotoPanMouseExecutionAction.EndPan,
            ShouldMarkHandled: true);
    }
}
