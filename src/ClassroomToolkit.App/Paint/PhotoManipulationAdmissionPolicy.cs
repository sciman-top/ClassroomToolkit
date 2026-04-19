namespace ClassroomToolkit.App.Paint;

internal static class PhotoManipulationAdmissionPolicy
{
    internal static PhotoManipulationEventHandlingPlan Resolve(
        bool photoModeActive,
        bool boardActive,
        PaintToolMode mode,
        bool inkOperationActive,
        bool photoPanning,
        int activeTouchCount)
    {
        var decision = PhotoManipulationRoutingPolicy.Resolve(
            photoModeActive,
            boardActive,
            mode,
            inkOperationActive,
            photoPanning,
            activeTouchCount);
        return PhotoManipulationEventHandlingPolicy.Resolve(decision);
    }
}
