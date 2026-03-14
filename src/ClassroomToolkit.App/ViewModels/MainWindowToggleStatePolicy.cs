namespace ClassroomToolkit.App.ViewModels;

internal readonly record struct MainWindowToggleState(
    bool IsPaintActive,
    bool IsRollCallVisible);

internal static class MainWindowToggleStatePolicy
{
    internal static MainWindowToggleState Resolve(
        bool overlayVisible,
        bool rollCallVisible)
    {
        return new MainWindowToggleState(
            IsPaintActive: overlayVisible,
            IsRollCallVisible: rollCallVisible);
    }
}
