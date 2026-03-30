using System.Windows;
using WpfPoint = System.Windows.Point;

namespace ClassroomToolkit.App.Paint;

internal static class CrossPageInputSwitchPolicy
{
    internal static bool ShouldSwitchByPointer(
        Rect currentPageRect,
        WpfPoint pointer,
        double hysteresisDip)
    {
        if (currentPageRect.IsEmpty || hysteresisDip <= CrossPageInputSwitchDefaults.MinPositiveHysteresisDip)
        {
            return !currentPageRect.Contains(pointer);
        }

        var expanded = currentPageRect;
        expanded.Inflate(hysteresisDip, hysteresisDip);
        return !expanded.Contains(pointer);
    }
}
