using System.Windows;

namespace ClassroomToolkit.App.Paint;

internal static class PresentationFollowMonitorPolicy
{
    /// <summary>
    /// 放映在副屏而覆盖层停留在主屏时，批注会画在放映画面之外。
    /// 进入放映全屏时覆盖层应搬到放映窗所在显示器；板书/照片模式有自己的
    /// 几何语义，不参与跟随。
    /// </summary>
    internal static bool ShouldFollow(
        bool photoModeActive,
        bool boardActive,
        bool overlayVisible,
        bool windowStateMinimized)
    {
        return !photoModeActive && !boardActive && overlayVisible && !windowStateMinimized;
    }

    internal static bool ShouldMove(Rect currentMonitorRect, Rect targetMonitorRect)
    {
        return !currentMonitorRect.Equals(targetMonitorRect);
    }
}
