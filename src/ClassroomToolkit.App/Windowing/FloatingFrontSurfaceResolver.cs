using System.Collections.Generic;

namespace ClassroomToolkit.App.Windowing;

internal static class FloatingFrontSurfaceResolver
{
    internal static ZOrderSurface Resolve(
        IWindowOrchestrator windowOrchestrator,
        IList<ZOrderSurface> surfaceStack,
        FloatingWindowRuntimeSnapshot snapshot)
    {
        windowOrchestrator.PruneSurfaceStack(
            surfaceStack,
            snapshot.PhotoActive,
            snapshot.PresentationFullscreen,
            snapshot.WhiteboardActive,
            snapshot.ImageManagerVisible);

        return windowOrchestrator.ResolveFrontSurface(
            surfaceStack as IReadOnlyList<ZOrderSurface> ?? new List<ZOrderSurface>(surfaceStack),
            snapshot.PhotoActive,
            snapshot.PresentationFullscreen,
            snapshot.WhiteboardActive,
            snapshot.ImageManagerVisible);
    }
}
