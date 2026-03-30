using System.Windows;

namespace ClassroomToolkit.App.Paint;

internal static class PhotoBackgroundVisibilityPolicy
{
    internal static Visibility Resolve(
        bool photoModeActive,
        bool boardActive,
        bool hasBackgroundSource)
    {
        return photoModeActive && !boardActive && hasBackgroundSource
            ? Visibility.Visible
            : Visibility.Collapsed;
    }
}
