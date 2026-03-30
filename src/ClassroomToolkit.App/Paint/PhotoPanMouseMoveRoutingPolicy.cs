using System.Windows.Input;

namespace ClassroomToolkit.App.Paint;

internal enum PhotoPanMouseMoveRoutingDecision
{
    PassThrough,
    UpdatePan,
    EndPan
}

internal static class PhotoPanMouseMoveRoutingPolicy
{
    internal static PhotoPanMouseMoveRoutingDecision Resolve(
        bool isMousePhotoPanActive,
        bool shouldAllowPhotoPan,
        MouseButtonState leftButton,
        MouseButtonState rightButton)
    {
        if (!isMousePhotoPanActive)
        {
            return PhotoPanMouseMoveRoutingDecision.PassThrough;
        }

        return PhotoPanTerminationPolicy.ShouldEndPan(
            shouldAllowPhotoPan,
            leftButton,
            rightButton)
            ? PhotoPanMouseMoveRoutingDecision.EndPan
            : PhotoPanMouseMoveRoutingDecision.UpdatePan;
    }
}
