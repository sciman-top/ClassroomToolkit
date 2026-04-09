using System.Windows.Input;
using ClassroomToolkit.App.Photos;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private void OnMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        var interactionState = CaptureInputInteractionState();
        var wheelRoute = OverlayInputRoutingPolicy.ResolveWheelRoute(
            interactionState.BoardActive,
            interactionState.PhotoModeActive,
            CanRoutePresentationInputFromOverlay(interactionState),
            PresentationChannelAvailabilityPolicy.IsAnyChannelEnabled(
                _presentationOptions.AllowOffice,
                _presentationOptions.AllowWps));
        if (wheelRoute == OverlayWheelInputRoute.ConsumeForBoard)
        {
            e.Handled = true;
            return;
        }
        if (wheelRoute == OverlayWheelInputRoute.HandlePhoto)
        {
            if (ShouldSuppressPhotoWheelFromRecentGesture())
            {
                LogPhotoInputTelemetry("wheel", "suppressed-recent-gesture");
                e.Handled = true;
                return;
            }
            ZoomPhoto(e.Delta, PhotoZoomAnchorPolicy.ResolveViewportCenter(OverlayRoot));
            LogPhotoInputTelemetry("wheel", $"delta={e.Delta}");
            e.Handled = true;
            return;
        }
        if (wheelRoute != OverlayWheelInputRoute.RoutePresentation)
        {
            return;
        }
        if (ShouldSuppressPresentationWheelFromRecentInkInput())
        {
            e.Handled = true;
            return;
        }
        var foregroundType = ResolveForegroundPresentationType();
        var presentationExecutionAction = OverlayWheelPresentationExecutionPolicy.Resolve(
            _wpsNavHookActive,
            _wpsHookInterceptWheel,
            _wpsHookBlockOnly,
            isWpsForeground: OverlayPresentationRouteContextBuilder.MapRouteType(foregroundType) == OverlayPresentationRouteType.Wps,
            WpsHookRecentlyFired(),
            e.Delta);
        var command = presentationExecutionAction switch
        {
            OverlayWheelPresentationExecutionAction.SendNext => ClassroomToolkit.Services.Presentation.PresentationCommand.Next,
            OverlayWheelPresentationExecutionAction.SendPrevious => ClassroomToolkit.Services.Presentation.PresentationCommand.Previous,
            _ => (ClassroomToolkit.Services.Presentation.PresentationCommand?)null
        };
        if (!command.HasValue)
        {
            return;
        }

        if (TrySendPresentationCommand(command.Value))
        {
            e.Handled = true;
        }
    }

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        var interactionState = CaptureInputInteractionState();
        var photoKeyHandled = TryHandlePhotoKey(e.Key);
        var keyRoute = OverlayInputRoutingPolicy.ResolveKeyRoute(
            _photoLoading,
            photoKeyHandled,
            interactionState.PhotoOrBoardActive,
            CanRoutePresentationInputFromOverlay(interactionState));
        if (keyRoute == OverlayKeyInputRoute.Consume)
        {
            e.Handled = true;
            return;
        }
        if (keyRoute != OverlayKeyInputRoute.RoutePresentation)
        {
            return;
        }
        if (TryHandlePresentationKey(e.Key))
        {
            e.Handled = true;
        }
    }

    private bool CanRoutePresentationInputFromOverlay()
    {
        return CanRoutePresentationInputFromOverlay(CaptureInputInteractionState());
    }

    private bool CanRoutePresentationInputFromOverlay(InputInteractionState interactionState)
    {
        return OverlayPresentationRoutingPolicy.CanRouteFromOverlay(
            _sessionCoordinator.CurrentState.NavigationMode,
            interactionState.PhotoModeActive,
            interactionState.BoardActive,
            _mode,
            _inputPassthroughEnabled);
    }

    public bool TryHandlePhotoKey(Key key)
    {
        var interactionState = CaptureInputInteractionState();
        if (!interactionState.PhotoNavigationEnabled)
        {
            return false;
        }
        if (key == Key.Escape && _photoFullscreen)
        {
            _photoFullscreen = false;
            SetPhotoWindowMode(fullscreen: false);
            return true;
        }
        if (IsPhotoNavigationKey(key, out var direction))
        {
            PhotoNavigationDiagnostics.Log("Overlay.Key", $"key={key}, dir={direction}, isPdf={_photoDocumentIsPdf}");
            HandlePhotoNavigationRequest(direction);
            return true;
        }
        if (key == Key.Add || key == Key.OemPlus)
        {
            ZoomPhotoByFactor(PhotoKeyZoomStep);
            return true;
        }
        if (key == Key.Subtract || key == Key.OemMinus)
        {
            ZoomPhotoByFactor(1.0 / PhotoKeyZoomStep);
            return true;
        }
        return false;
    }
}
