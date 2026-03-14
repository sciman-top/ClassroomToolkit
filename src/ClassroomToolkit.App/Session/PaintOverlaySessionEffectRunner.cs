using System;

namespace ClassroomToolkit.App.Session;

public sealed class PaintOverlaySessionEffectRunner : IUiSessionEffectRunner
{
    private readonly Action<bool> _applyOverlayTopmost;
    private readonly Action<UiNavigationMode> _applyNavigationMode;
    private readonly Action<UiInkVisibility> _applyInkVisibility;
    private readonly Action<UiSessionWidgetVisibility> _applyWidgetVisibility;
    private readonly Action<UiSessionTransition>? _onTransition;

    public PaintOverlaySessionEffectRunner(
        Action<bool> applyOverlayTopmost,
        Action<UiNavigationMode> applyNavigationMode,
        Action<UiInkVisibility> applyInkVisibility,
        Action<UiSessionWidgetVisibility>? applyWidgetVisibility = null,
        Action<UiSessionTransition>? onTransition = null)
    {
        _applyOverlayTopmost = applyOverlayTopmost ?? throw new ArgumentNullException(nameof(applyOverlayTopmost));
        _applyNavigationMode = applyNavigationMode ?? throw new ArgumentNullException(nameof(applyNavigationMode));
        _applyInkVisibility = applyInkVisibility ?? throw new ArgumentNullException(nameof(applyInkVisibility));
        _applyWidgetVisibility = applyWidgetVisibility ?? (_ => { });
        _onTransition = onTransition;
    }

    public void Run(UiSessionTransition transition)
    {
        if (transition == null)
        {
            return;
        }

        if (transition.Previous.OverlayTopmostRequired != transition.Current.OverlayTopmostRequired
            || transition.Previous.Scene != transition.Current.Scene)
        {
            _applyOverlayTopmost(transition.Current.OverlayTopmostRequired);
        }

        if (transition.Previous.NavigationMode != transition.Current.NavigationMode)
        {
            _applyNavigationMode(transition.Current.NavigationMode);
        }

        if (transition.Previous.InkVisibility != transition.Current.InkVisibility)
        {
            _applyInkVisibility(transition.Current.InkVisibility);
        }

        if (transition.Previous.RollCallVisible != transition.Current.RollCallVisible
            || transition.Previous.LauncherVisible != transition.Current.LauncherVisible
            || transition.Previous.ToolbarVisible != transition.Current.ToolbarVisible)
        {
            _applyWidgetVisibility(new UiSessionWidgetVisibility(
                transition.Current.RollCallVisible,
                transition.Current.LauncherVisible,
                transition.Current.ToolbarVisible));
        }

        _onTransition?.Invoke(transition);
    }
}
