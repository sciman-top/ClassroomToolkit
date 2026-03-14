using System.Windows;
using WpfPoint = System.Windows.Point;

namespace ClassroomToolkit.App.Paint;

internal static class CrossPageInputSwitchRequestPolicy
{
    internal static bool ShouldSwitchForInput(
        bool photoModeActive,
        bool crossPageDisplayEnabled,
        bool boardActive,
        PaintToolMode mode,
        bool photoPanning,
        bool crossPageDragging,
        bool hasBitmap,
        Rect? currentPageRect,
        WpfPoint pointer,
        double pointerHysteresisDip)
    {
        var canSwitchByGate = CrossPageInputSwitchGatePolicy.CanSwitchForInput(
            photoModeActive,
            crossPageDisplayEnabled,
            boardActive,
            mode,
            photoPanning,
            crossPageDragging);
        var hasCurrentRect = currentPageRect.HasValue;
        var shouldSwitchByPointer = true;
        if (hasCurrentRect && currentPageRect is Rect resolvedRect)
        {
            shouldSwitchByPointer = CrossPageInputSwitchPolicy.ShouldSwitchByPointer(
                resolvedRect,
                pointer,
                pointerHysteresisDip);
        }
        return CrossPageInputSwitchAdmissionPolicy.ShouldProceed(
            canSwitchByGate,
            hasBitmap,
            hasCurrentRect,
            shouldSwitchByPointer);
    }
}
