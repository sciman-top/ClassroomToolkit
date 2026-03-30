using System.Collections.Generic;

namespace ClassroomToolkit.App.Windowing;

public interface IWindowOrchestrator
{
    bool TouchSurface(IList<ZOrderSurface> surfaceStack, ZOrderSurface surface);

    void PruneSurfaceStack(
        IList<ZOrderSurface> surfaceStack,
        bool photoActive,
        bool presentationFullscreen,
        bool whiteboardActive,
        bool imageManagerVisible);

    ZOrderSurface ResolveFrontSurface(
        IReadOnlyList<ZOrderSurface> surfaceStack,
        bool photoActive,
        bool presentationFullscreen,
        bool whiteboardActive,
        bool imageManagerVisible);
}
