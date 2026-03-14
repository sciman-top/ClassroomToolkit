using System.Windows.Input;

namespace ClassroomToolkit.App.Paint;

internal readonly record struct PhotoTitleBarDragZOrderPlan(
    bool CanDrag,
    bool RequestZOrderBeforeDrag,
    bool RequestZOrderAfterDrag,
    bool ForceAfterDrag);

internal static class PhotoTitleBarDragZOrderPolicy
{
    internal static PhotoTitleBarDragZOrderPlan Resolve(
        bool photoModeActive,
        bool photoFullscreen,
        MouseButton changedButton)
    {
        var canDrag = photoModeActive
            && !photoFullscreen
            && changedButton == MouseButton.Left;
        if (!canDrag)
        {
            return new PhotoTitleBarDragZOrderPlan(
                CanDrag: false,
                RequestZOrderBeforeDrag: false,
                RequestZOrderAfterDrag: false,
                ForceAfterDrag: false);
        }

        return new PhotoTitleBarDragZOrderPlan(
            CanDrag: true,
            RequestZOrderBeforeDrag: false,
            RequestZOrderAfterDrag: true,
            ForceAfterDrag: false);
    }
}

