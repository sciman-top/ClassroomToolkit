using System;
using System.Windows;
using ClassroomToolkit.App.Photos;
using ClassroomToolkit.App.Utilities;
using ClassroomToolkit.App.Settings;
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
            _lastPhotoStudentId = null;
            return;
        }
        var studentId = _viewModel.CurrentStudentId;
        if (string.IsNullOrWhiteSpace(studentId))
        {
            HidePhotoOverlay();
            _lastPhotoStudentId = null;
            return;
        }
        
        _lastPhotoStudentId = studentId;
        var resolver = EnsurePhotoResolver();
        var className = ResolvePhotoClassName();
        var path = resolver.ResolvePhotoPath(className, studentId);
        if (string.IsNullOrWhiteSpace(path))
        {
            HidePhotoOverlay();
            return;
        }
        var overlay = EnsurePhotoOverlay();
        overlay.ShowPhoto(path, _viewModel.CurrentStudentName, _viewModel.CurrentStudentId, _viewModel.PhotoDurationSeconds, this);
    }

    private StudentPhotoResolver EnsurePhotoResolver()
    {
        if (_photoResolver != null)
        {
            return _photoResolver;
        }
        var root = ResolvePhotoRoot();
        _photoResolver = new StudentPhotoResolver(root);
        return _photoResolver;
    }

    private string ResolvePhotoRoot()
    {
        return StudentResourceLocator.ResolveStudentPhotoRoot();
    }

    private string ResolvePhotoClassName()
    {
        if (!string.IsNullOrWhiteSpace(_viewModel.PhotoSharedClass))
        {
            return _viewModel.PhotoSharedClass;
        }
        return _viewModel.ActiveClassName;
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
        // Do not invalidate resolver cache on regular overlay close.
        // Frequent close/show cycles are part of normal roll-call flow; clearing the whole
        // class index here causes repeated directory re-index and visible latency spikes.
    }
}
