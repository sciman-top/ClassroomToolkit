using System;
using System.Windows;
using ClassroomToolkit.App.Photos;
using ClassroomToolkit.App.Utilities;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App;

public partial class RollCallWindow
{
    private void UpdatePhotoDisplay(bool forceHide = false)
    {
        if (forceHide || !_viewModel.ShowPhoto || !_viewModel.IsRollCallMode)
        {
            HidePhotoOverlay();
            return;
        }
        var studentId = _viewModel.CurrentStudentId;
        if (string.IsNullOrWhiteSpace(studentId))
        {
            HidePhotoOverlay();
            return;
        }

        // Keep overlay source consistent with in-window preview source.
        // This avoids dual-path divergence between minimized and non-minimized states.
        var path = _viewModel.CurrentStudentPhotoPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            PhotoOverlayDiagnostics.Log(
                "rollcall-skip",
                $"studentId={studentId} reason=no-photo-path");
            HidePhotoOverlay();
            return;
        }
        PhotoOverlayDiagnostics.Log(
            "rollcall-request",
            $"studentId={studentId} path={System.IO.Path.GetFileName(path)} duration={_viewModel.PhotoDurationSeconds} rollVisible={IsVisible} rollState={WindowState} groupOverlayVisible={_groupOverlay?.IsVisible == true}");
        RecreateHiddenPhotoOverlayIfNeeded();
        var overlay = EnsurePhotoOverlay();
        overlay.ShowPhoto(path, _viewModel.CurrentStudentName, _viewModel.CurrentStudentId, _viewModel.PhotoDurationSeconds, this);
    }

    private void RecreateHiddenPhotoOverlayIfNeeded()
    {
        if (_photoOverlay == null || _photoOverlay.IsVisible)
        {
            return;
        }

        var overlay = _photoOverlay;
        _photoOverlay = null;
        overlay.PhotoClosed -= OnPhotoClosed;
        SafeActionExecutionExecutor.TryExecute(
            overlay.Close,
            ex => System.Diagnostics.Debug.WriteLine(
                RollCallWindowDiagnosticsPolicy.FormatPhotoOverlayCloseFailureMessage(
                    "recreate-hidden-overlay",
                    ex.GetType().Name,
                    ex.Message)));
        PhotoOverlayDiagnostics.Log("overlay-recreate", "reason=hidden-window-recreate");
    }

    private PhotoOverlayWindow EnsurePhotoOverlay()
    {
        if (_photoOverlay != null)
        {
            return _photoOverlay;
        }
        _photoOverlay = new PhotoOverlayWindow();
        _photoOverlay.PhotoClosed += OnPhotoClosed;
        return _photoOverlay;
    }

    private void HidePhotoOverlay()
    {
        if (_photoOverlay == null)
        {
            return;
        }
        SafeActionExecutionExecutor.TryExecute(
            _photoOverlay.CloseOverlay,
            ex => System.Diagnostics.Debug.WriteLine(
                RollCallWindowDiagnosticsPolicy.FormatPhotoOverlayCloseFailureMessage(
                    "hide-overlay",
                    ex.GetType().Name,
                    ex.Message)));
    }

    private void ClosePhotoOverlay()
    {
        if (_photoOverlay == null)
        {
            return;
        }
        SafeActionExecutionExecutor.TryExecute(
            _photoOverlay.CloseOverlay,
            ex => System.Diagnostics.Debug.WriteLine(
                RollCallWindowDiagnosticsPolicy.FormatPhotoOverlayCloseFailureMessage(
                    "close-overlay",
                    ex.GetType().Name,
                    ex.Message)));
        SafeActionExecutionExecutor.TryExecute(
            _photoOverlay.Close,
            ex => System.Diagnostics.Debug.WriteLine(
                RollCallWindowDiagnosticsPolicy.FormatPhotoOverlayCloseFailureMessage(
                    "close-window",
                    ex.GetType().Name,
                    ex.Message)));
        _photoOverlay.PhotoClosed -= OnPhotoClosed;
        _photoOverlay = null;
    }

    private void OnPhotoClosed(string? studentId)
    {
        _ = studentId;
    }
}
