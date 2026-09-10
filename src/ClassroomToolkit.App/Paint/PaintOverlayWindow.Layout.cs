using System;
using System.Windows;
using System.Windows.Threading;
using ClassroomToolkit.App.Session;
using ClassroomToolkit.App.Windowing;
using ClassroomToolkit.Interop.Presentation;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private void UpdateInputPassthrough()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }
        var enable = OverlayInputPassthroughPolicy.ShouldEnable(
            _mode,
            _boardOpacity,
            _photoModeActive);
        _inputPassthroughEnabled = enable;
        ApplyWindowStyles();
        UpdateFocusAcceptance();
    }

    private void UpdateOverlayHitTestVisibility()
    {
        if (OverlayRoot == null)
        {
            return;
        }

        OverlayRoot.IsHitTestVisible = OverlayHitTestPolicy.ShouldEnableOverlayHitTest(
            _mode,
            _photoModeActive,
            _photoLoading);
    }

    private void UpdateFocusAcceptance()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }
        var blockFocus = ShouldBlockFocus();
        if (_focusBlocked == blockFocus)
        {
            return;
        }
        _focusBlocked = blockFocus;
        ApplyWindowStyles();
    }

    private bool ShouldBlockFocus()
    {
        var navigationMode = _sessionCoordinator.CurrentState.NavigationMode;
        var presentationAllowed = PresentationChannelAvailabilityPolicy.IsAnyChannelEnabled(
            _presentationOptions.AllowOffice,
            _presentationOptions.AllowWps);
        var allowResolver = OverlayFocusResolverGatePolicy.ShouldResolvePresentationTarget(
            presentationAllowed,
            UiSessionPresentationInputPolicy.AllowsPresentationInput(navigationMode));
        var (presentationTargetValid, wpsRawTargetValid) = ResolvePresentationFocusTargets(allowResolver);

        return OverlayFocusAcceptancePolicy.ShouldBlockFocus(
            navigationMode,
            _inputPassthroughEnabled,
            _mode,
            _photoModeActive,
            IsBoardActive(),
            presentationAllowed,
            presentationTargetValid,
            wpsRawTargetValid);
    }

    private (bool PresentationTargetValid, bool WpsRawTargetValid) ResolvePresentationFocusTargets(bool allowResolver)
    {
        if (!allowResolver)
        {
            return (false, false);
        }

        var wpsTarget = _presentationOptions.AllowWps
            ? ResolveWpsTarget()
            : PresentationTarget.Empty;
        var officeTarget = _presentationOptions.AllowOffice
            ? ResolveOfficeTarget()
            : PresentationTarget.Empty;
        var presentationTargetValid = wpsTarget.IsValid || officeTarget.IsValid;
        if (!WpsRawFallbackTargetPolicy.ShouldResolveWpsRawTarget(
                presentationTargetValid,
                _presentationOptions.AllowWps))
        {
            return (presentationTargetValid, false);
        }

        var wpsFallbackTarget = ResolveWpsTarget();
        var wpsRawTargetValid = WpsRawFallbackTargetPolicy.IsValid(
            wpsFallbackTarget.IsValid,
            ResolveWpsSendMode(wpsFallbackTarget));
        return (presentationTargetValid, wpsRawTargetValid);
    }

    private void ApplyWindowStyles()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }
        if (!OverlayWindowStyleApplyPolicy.ShouldApply(
                _inputPassthroughEnabled,
                _focusBlocked,
                _lastAppliedInputPassthroughEnabled,
                _lastAppliedFocusBlocked))
        {
            return;
        }
        var styleMask = OverlayWindowStyleBitsPolicy.Resolve(_inputPassthroughEnabled, _focusBlocked);

        var updated = WindowStyleExecutor.TryUpdateStyleBits(
            _hwnd,
            WindowStyleIndexPolicy.ExStyle,
            styleMask.SetMask,
            styleMask.ClearMask,
            out _);
        if (updated)
        {
            _lastAppliedInputPassthroughEnabled = _inputPassthroughEnabled;
            _lastAppliedFocusBlocked = _focusBlocked;
        }
    }

    private bool TryBeginInvoke(Action action, DispatcherPriority priority)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (!DispatcherInvokeAvailabilityPolicy.CanBeginInvoke(
                Dispatcher.HasShutdownStarted,
                Dispatcher.HasShutdownFinished))
        {
            return false;
        }
        return PaintActionInvoker.TryInvoke(() =>
        {
            Dispatcher.BeginInvoke(action, priority);
            return true;
        }, fallback: false);
    }

    private void RecoverOverlayFullscreenBounds()
    {
        OverlayFullscreenBoundsRecoveryExecutor.Apply(
            shouldRecover: true,
            normalizeWindowState: NormalizeOverlayWindowState,
            applyImmediateBounds: ApplyFullscreenBounds,
            applyDeferredBounds: RequestDeferredFullscreenBoundsRecovery);
    }

    private void NormalizeOverlayWindowState(bool shouldNormalize)
    {
        WindowStateNormalizationExecutor.Apply(this, shouldNormalize);
    }

    private void RequestDeferredFullscreenBoundsRecovery()
    {
        var scheduled = TryBeginInvoke(ApplyFullscreenBounds, DispatcherPriority.Background);
        if (!scheduled && Dispatcher.CheckAccess())
        {
            ApplyFullscreenBounds();
        }
    }
}
