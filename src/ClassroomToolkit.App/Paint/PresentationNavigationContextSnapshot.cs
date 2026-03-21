using ClassroomToolkit.App.Session;
using ClassroomToolkit.Interop.Presentation;

namespace ClassroomToolkit.App.Paint;

internal readonly record struct PresentationNavigationContextSnapshot(
    PaintToolMode Mode,
    UiNavigationMode NavigationMode,
    PresentationType ForegroundPresentationType,
    PresentationType CurrentPresentationType,
    bool AllowWps,
    bool AllowOffice,
    bool PresentationFullscreenActive)
{
    internal static readonly PresentationNavigationContextSnapshot Default = new(
        Mode: PaintToolMode.Brush,
        NavigationMode: UiNavigationMode.Disabled,
        ForegroundPresentationType: PresentationType.None,
        CurrentPresentationType: PresentationType.None,
        AllowWps: false,
        AllowOffice: false,
        PresentationFullscreenActive: false);
}
