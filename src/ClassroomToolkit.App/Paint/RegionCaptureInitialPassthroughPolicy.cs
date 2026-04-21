using System.Drawing;

namespace ClassroomToolkit.App.Paint;

internal readonly record struct RegionCaptureInitialPassthroughDecision(
    bool ShouldCancel,
    RegionScreenCapturePassthroughInputKind InputKind,
    Point? ScreenPoint);

internal static class RegionCaptureInitialPassthroughPolicy
{
    internal static RegionCaptureInitialPassthroughDecision Resolve(
        int pointerScreenX,
        int pointerScreenY,
        IReadOnlyCollection<Rectangle>? passthroughRegions)
    {
        if (passthroughRegions == null)
        {
                return new RegionCaptureInitialPassthroughDecision(
                    ShouldCancel: false,
                    InputKind: RegionScreenCapturePassthroughInputKind.None,
                    ScreenPoint: null);
        }

        foreach (var region in passthroughRegions)
        {
            if (region.Width <= 0 || region.Height <= 0)
            {
                continue;
            }

            if (region.Contains(pointerScreenX, pointerScreenY))
            {
                return new RegionCaptureInitialPassthroughDecision(
                    ShouldCancel: true,
                    InputKind: RegionScreenCapturePassthroughInputKind.PointerMove,
                    ScreenPoint: new Point(pointerScreenX, pointerScreenY));
            }
        }

        return new RegionCaptureInitialPassthroughDecision(
            ShouldCancel: false,
            InputKind: RegionScreenCapturePassthroughInputKind.None,
            ScreenPoint: null);
    }
}
