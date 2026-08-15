using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.Photos;
using ClassroomToolkit.App.Presentation;
using ClassroomToolkit.Application.UseCases.Photos;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App;

/// <summary>
/// Photo teaching, image manager, photo navigation, and presentation foreground management.
/// </summary>
public partial class MainWindow
{
    private bool IsOverlayVisibleForWindowing() => _overlayWindow?.IsVisible == true;
    private bool OverlayExistsForSurfaceTransition() => _overlayWindow != null;
    private ForegroundSurfaceActivityState CaptureForegroundSurfaceActivityState()
    {
        return new ForegroundSurfaceActivityState(
            OverlayExists: OverlayExistsForSurfaceTransition(),
            PhotoModeActive: _overlayWindow?.IsPhotoModeActive == true,
            WhiteboardActive: _toolbarWindow?.BoardActive == true);
    }

    private void ApplyImageManagerSurfaceTransition(ImageManagerSurfaceTransitionKind kind)
    {
        ApplySurfaceZOrderDecision(
            ImageManagerSurfaceTransitionPolicy.Resolve(
                kind,
                overlayVisible: IsOverlayVisibleForWindowing()));
    }

    private void ApplyPhotoModeSurfaceTransition(
        PhotoModeSurfaceTransitionKind kind,
        bool photoModeActive,
        bool requestZOrderApply,
        bool forceEnforceZOrder)
    {
        var context = new PhotoModeSurfaceTransitionContext(
            PhotoModeActive: photoModeActive,
            RequestZOrderApply: requestZOrderApply,
            ForceEnforceZOrder: forceEnforceZOrder,
            OverlayVisible: IsOverlayVisibleForWindowing());
        ApplySurfaceZOrderDecision(
            PhotoModeSurfaceTransitionPolicy.Resolve(
                kind,
                context));
    }

    private void OnImageSelected(IReadOnlyList<string> images, int index)
    {
        PhotoNavigationDiagnostics.Log("MainWindow.Select", $"count={images.Count}, index={index}");
        if (_overlayWindow == null)
        {
            EnsurePaintWindows();
        }
        if (_overlayWindow == null)
        {
            return;
        }
        var selectionPlan = PhotoSelectionPreparationPolicy.Resolve(
            imageManagerVisible: _imageManagerWindow?.IsVisible == true,
            whiteboardActive: _toolbarWindow?.BoardActive == true);
        // Capture "显示笔迹" state before closing ImageManager (Closed handler nullifies the reference)
        var showInk = _imageManagerWindow?.ViewModel?.ShowInkOverlay ?? _settings.PhotoShowInkOverlay;
        if (PhotoShowInkOverlayChangePolicy.ShouldApply(_settings.PhotoShowInkOverlay, showInk))
        {
            _settings.PhotoShowInkOverlay = showInk;
            SaveSettings();
        }
        if (!PreparePhotoSelectionTransition(selectionPlan))
        {
            return;
        }
        var overlay = _overlayWindow;
        if (overlay == null)
        {
            return;
        }
        _photoNavigationSession.Reset(images, index);
        var selectedPath = _photoNavigationSession.GetCurrentPath();
        ApplyPhotoOverlayEntry(
            overlay,
            selectedPath,
            showInk,
            allowInkOutsidePhoto: false,
            preserveImageOriginalScale: false,
            logAction: path => PhotoNavigationDiagnostics.Log("MainWindow.Select", $"enter path={path}"));
    }

    private void ApplyPhotoOverlayEntry(
        Paint.PaintOverlayWindow overlay,
        string? path,
        bool showInk,
        bool allowInkOutsidePhoto,
        bool preserveImageOriginalScale,
        Action<string> logAction)
    {
        var entryPlan = PhotoOverlayEntryPolicy.Resolve(!string.IsNullOrWhiteSpace(path));
        if (entryPlan.UpdateSequence)
        {
            overlay.SetPhotoSequence(_photoNavigationSession.Sequence, _photoNavigationSession.CurrentIndex);
        }
        if (!entryPlan.EnterPhotoMode || string.IsNullOrWhiteSpace(path))
        {
            overlay.SetPhotoInkCanvasUnbounded(allowInkOutsidePhoto);
            return;
        }
        if (entryPlan.UpdateInkVisibility)
        {
            overlay.UpdateInkShowEnabled(showInk);
        }
        if (entryPlan.SuppressNextOverlayActivatedApply)
        {
            OverlayActivatedRetouchStateUpdater.MarkSuppressNextApply(ref _overlayActivatedRetouchState);
        }
        logAction(path);
        overlay.EnterPhotoMode(path);
        overlay.SetPhotoInkCanvasUnbounded(allowInkOutsidePhoto);
        if (preserveImageOriginalScale)
        {
            overlay.CenterPhotoAtOriginalScale();
        }
        ApplySurfaceZOrderDecision(
            PhotoOverlayEntrySurfaceTransitionPolicy.Resolve(entryPlan.TouchPhotoSurface));
        if (entryPlan.FocusOverlay)
        {
            FocusOverlayForPhotoNavigation(defer: true, avoidActivate: true);
        }
    }

    private void ApplyImageManagerCloseForPhotoSelection()
    {
        var imageManagerWindow = _imageManagerWindow;
        if (imageManagerWindow == null)
        {
            return;
        }

        var closeContext = new ImageManagerVisibilityCloseContext(
            ImageManagerVisible: imageManagerWindow.IsVisible,
            OwnerAlreadyOverlay: imageManagerWindow.Owner == _overlayWindow && _overlayWindow != null);
        var closePlan = ImageManagerVisibilityTransitionPolicy.ResolveCloseForPhotoSelection(closeContext);
        ImageManagerVisibilityTransitionCoordinator.ApplyCloseForPhotoSelection(
            closePlan,
            () => DetachOverlayOwnedWindow(imageManagerWindow),
            () => SafeActionExecutionExecutor.TryExecute(
                imageManagerWindow.Close,
                ex => PhotoNavigationDiagnostics.Log(
                    "MainWindow.Select",
                    LifecycleSafeExecutionDiagnosticsPolicy.FormatFailureMessage(
                        "photo-selection",
                        "close-image-manager-window",
                        ex.GetType().Name,
                        ex.Message))));
    }

    private bool PreparePhotoSelectionTransition(PhotoSelectionPreparationPlan selectionPlan)
    {
        if (selectionPlan.CloseImageManager)
        {
            // 全屏展示时关闭管理窗口，避免其继续吃键盘事件。
            PhotoNavigationDiagnostics.Log("MainWindow.Select", "close ImageManager");
            ApplyImageManagerCloseForPhotoSelection();
        }

        ShowPaintOverlayIfNeeded();
        if (_overlayWindow == null)
        {
            return false;
        }

        if (selectionPlan.DisableWhiteboard)
        {
            _toolbarWindow?.SetBoardActive(false);
        }
        if (selectionPlan.SuppressPresentationForeground)
        {
            BeginPresentationForegroundSuppression(
                TimeSpan.FromMilliseconds(selectionPlan.PresentationForegroundSuppressionMs));
        }

        return true;
    }

    private void OnPhotoNavigateRequested(int direction)
    {
        if (_overlayWindow == null)
        {
            return;
        }
        PhotoNavigationDiagnostics.Log(
            "MainWindow.FileNav",
            $"dir={direction}, overlayPath={_overlayWindow.CurrentDocumentPath}, overlayType={_overlayWindow.CurrentPhotoFileType}, sessionIndex={_photoNavigationSession.CurrentIndex}, sessionCount={_photoNavigationSession.Sequence.Count}");
        var decision = _photoNavigationSession.Plan(
            _overlayWindow.CurrentDocumentPath,
            direction,
            _overlayWindow.CurrentPhotoFileType);

        // Keep index aligned with the actual page shown in overlay.
        _photoNavigationSession.SyncResolvedIndex(decision);
        PhotoNavigationDiagnostics.Log(
            "MainWindow.FileNav",
            $"decision navigate={decision.ShouldNavigateFile}, resolved={decision.ResolvedCurrentIndex}, next={decision.NextIndex}, currentType={decision.CurrentFileType}");

        if (!_photoNavigationSession.TryApplyFileNavigation(decision, out var nextPath)
            || string.IsNullOrWhiteSpace(nextPath))
        {
            PhotoNavigationDiagnostics.Log("MainWindow.FileNav", "skip file navigation");
            return;
        }

        // 切换到序列中的下一个文件（由统一策略决策）
        ApplyPhotoOverlayEntry(
            _overlayWindow,
            nextPath,
            _settings.PhotoShowInkOverlay,
            allowInkOutsidePhoto: false,
            preserveImageOriginalScale: false,
            logAction: path => PhotoNavigationDiagnostics.Log("MainWindow.FileNav", $"enter nextPath={path}"));
    }

    private void BeginPresentationForegroundSuppression(TimeSpan duration)
    {
        if (_presentationForegroundSuppression == null)
        {
            _presentationForegroundSuppression = PresentationForegroundSuppressionInteropAdapter.SuppressForeground();
        }
        _presentationForegroundSuppressionTimer.Stop();
        _presentationForegroundSuppressionTimer.Interval = duration;
        _presentationForegroundSuppressionTimer.Start();
    }

    private void ReleasePresentationForegroundSuppression()
    {
        _presentationForegroundSuppressionTimer.Stop();
        _presentationForegroundSuppression?.Dispose();
        _presentationForegroundSuppression = null;
    }

    private void OnPhotoModeChanged(bool active)
    {
        if (_overlayWindow == null || _toolbarWindow == null)
        {
            return;
        }
        var transitionPlan = PaintVisibilityTransitionPolicy.ResolvePhotoModeChange(
            photoModeActive: active,
            toolbarWindowState: _toolbarWindow.WindowState);
        var toolbarWindow = _toolbarWindow;
        PhotoModeTransitionCoordinator.Apply(
            active,
            transitionPlan,
            value => _imageManagerWindow?.SetKeyboardNavigationSuppressed(value),
            () => WindowStateNormalizationExecutor.Apply(toolbarWindow, transitionPlan.NormalizeToolbarWindowState),
            () => ExecuteLifecycleSafe("photo-mode-changed", "show-toolbar-window", toolbarWindow.Show),
            () => SyncFloatingWindowOwners(overlayVisible: transitionPlan.SyncFloatingOwnersVisible),
            () => ApplyPhotoModeSurfaceTransition(
                PhotoModeSurfaceTransitionKind.PhotoModeChanged,
                photoModeActive: active,
                requestZOrderApply: transitionPlan.RequestZOrderApply,
                forceEnforceZOrder: transitionPlan.ForceEnforceZOrder));
    }

    private void OnPhotoCursorModeFocusRequested()
    {
        if (!PhotoCursorModeFocusPolicy.ShouldFocusOverlay(_overlayWindow?.IsPhotoModeActive == true))
        {
            return;
        }

        FocusOverlayForPhotoNavigation(defer: true, avoidActivate: true);
    }

    private void FocusOverlayForPhotoNavigation(bool defer, bool avoidActivate = false)
    {
        if (_overlayWindow == null)
        {
            return;
        }

        void FocusNow()
        {
            if (_overlayWindow == null || !_overlayWindow.IsVisible)
            {
                return;
            }

            var focusPlanDecision = OverlayNavigationFocusPolicy.ResolvePlanDecision(
                avoidActivate,
                CaptureOverlayNavigationFocusSnapshot(_overlayWindow));
            var focusPlan = focusPlanDecision.Plan;

            OverlayFocusExecutionExecutor.Apply(
                _overlayWindow,
                focusPlan.ActivateOverlay,
                focusPlan.KeyboardFocusOverlay);

            PhotoNavigationDiagnostics.Log(
                "MainWindow.Focus",
                $"defer={defer}, activate={focusPlan.ActivateOverlay}, keyboard={focusPlan.KeyboardFocusOverlay}, activateReason={focusPlanDecision.ActivateReason}, keyboardReason={focusPlanDecision.KeyboardFocusReason}");
        }

        if (defer)
        {
            var scheduled = TryBeginInvoke(
                FocusNow,
                DispatcherPriority.Input,
                "FocusOverlayForPhotoNavigation");
            if (!scheduled)
            {
                if (Dispatcher.CheckAccess())
                {
                    FocusNow();
                }
            }
            return;
        }

        FocusNow();
    }

    private OverlayNavigationFocusSnapshot CaptureOverlayNavigationFocusSnapshot(
        Paint.PaintOverlayWindow overlayWindow)
    {
        return OverlayNavigationFocusSnapshotPolicy.Resolve(
            overlayVisible: overlayWindow.IsVisible,
            overlayActive: overlayWindow.IsActive,
            utilityActivity: CaptureFloatingUtilityActivity());
    }

    private void OnOverlayActivated()
    {
        var activityState = CaptureForegroundSurfaceActivityState();
        var decision = ForegroundSurfaceTransitionPolicy.Resolve(
            ForegroundSurfaceTransitionKind.OverlayActivated,
            suppressNextApply: _overlayActivatedRetouchState.SuppressNextApply,
            activityState,
            surface: ZOrderSurface.None);
        var result = ForegroundSurfaceRetouchCoordinator.ApplyOverlayActivated(
            suppressionConsumed: OverlayActivatedRetouchStateUpdater.TryConsumeSuppression(ref _overlayActivatedRetouchState),
            decision,
            _overlayActivatedRetouchState.LastRetouchUtc,
            GetCurrentUtcTimestamp(),
            MainWindowRuntimeDefaults.OverlayActivationRetouchMinIntervalMs,
            nowUtc => OverlayActivatedRetouchStateUpdater.MarkRetouched(
                ref _overlayActivatedRetouchState,
                nowUtc),
            ApplyForegroundSurfaceDecision);
        if (result.SuppressionConsumed)
        {
            return;
        }

        if (!result.Applied)
        {
            System.Diagnostics.Debug.WriteLine(
                OverlayActivationDiagnosticsPolicy.FormatRetouchSkipMessage(
                    result.Reason));
        }
    }

    private void OnPresentationFullscreenDetected()
    {
        ApplyPhotoModeSurfaceTransition(
            PhotoModeSurfaceTransitionKind.PresentationFullscreenDetected,
            photoModeActive: false,
            requestZOrderApply: false,
            forceEnforceZOrder: false);
    }

    private void OnPresentationForegroundDetected(PresentationForegroundSource type)
    {
        ApplyExplicitForegroundRetouch(ZOrderSurface.PresentationFullscreen);
    }

    private void OnPhotoForegroundDetected()
    {
        ApplyExplicitForegroundRetouch(ZOrderSurface.PhotoFullscreen);
    }

    private void ApplyForegroundSurfaceDecision(SurfaceZOrderDecision decision)
    {
        ApplySurfaceZOrderDecision(decision);
    }

    private void ApplyExplicitForegroundRetouch(ZOrderSurface surface)
    {
        var decision = ForegroundSurfaceTransitionPolicy.Resolve(
            ForegroundSurfaceTransitionKind.ExplicitForeground,
            activityState: CaptureForegroundSurfaceActivityState(),
            surface: surface,
            suppressNextApply: false);
        var result = ForegroundSurfaceRetouchCoordinator.ApplyExplicitForeground(
            _explicitForegroundRetouchState,
            GetCurrentUtcTimestamp(),
            MainWindowRuntimeDefaults.ExplicitForegroundRetouchMinIntervalMs,
            decision,
            nowUtc => ExplicitForegroundRetouchStateUpdater.MarkRetouched(
                ref _explicitForegroundRetouchState,
                nowUtc),
            ApplyForegroundSurfaceDecision);
        if (!result.Applied)
        {
            System.Diagnostics.Debug.WriteLine(
                ForegroundExplicitRetouchDiagnosticsPolicy.FormatThrottleSkipMessage(
                    surface,
                    result.Reason));
        }
    }

    private void OnPhotoUnifiedTransformChanged(
        double scaleX,
        double scaleY,
        double translateX,
        double translateY)
    {
        if (!_settings.PhotoRememberTransform)
        {
            return;
        }

        var changed = PhotoUnifiedTransformChangePolicy.HasChanged(
            _settings.PhotoUnifiedTransformEnabled,
            _settings.PhotoUnifiedScaleX,
            _settings.PhotoUnifiedScaleY,
            _settings.PhotoUnifiedTranslateX,
            _settings.PhotoUnifiedTranslateY,
            scaleX,
            scaleY,
            translateX,
            translateY,
            MainWindowRuntimeDefaults.NumericComparisonEpsilon);

        _settings.PhotoUnifiedTransformEnabled = true;
        _settings.PhotoUnifiedScaleX = scaleX;
        _settings.PhotoUnifiedScaleY = scaleY;
        _settings.PhotoUnifiedTranslateX = translateX;
        _settings.PhotoUnifiedTranslateY = translateY;

        if (changed)
        {
            SaveSettings();
        }
    }

    internal bool TryHandleOverlayNavigationKeyFromAuxWindow(Key key)
    {
        var overlay = _overlayWindow;
        if (overlay == null)
        {
            return false;
        }

        return Paint.AuxWindowKeyRoutingHandler.TryHandle(
            key,
            overlayVisible: overlay.IsVisible,
            tryHandlePhotoKey: overlay.TryHandlePhotoKey,
            canRoutePresentationInput: overlay.CanRoutePresentationInputFromAuxWindow(),
            tryForwardPresentationKey: overlay.ForwardKeyboardToPresentation);
    }

    internal bool TryHandleOverlayNavigationWheelFromAuxWindow(int delta)
    {
        var overlay = _overlayWindow;
        if (overlay == null)
        {
            return false;
        }

        return Paint.AuxWindowWheelRoutingHandler.TryHandle(
            delta,
            overlayVisible: overlay.IsVisible,
            canRoutePresentationInput: overlay.CanRoutePresentationInputFromAuxWindow(),
            tryForwardPresentationWheel: overlay.ForwardWheelToPresentation);
    }
}
