using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ClassroomToolkit.Interop.Presentation;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    public bool RestorePresentationFocusIfNeeded(bool requireFullscreen = false)
    {
        if (_photoModeActive || IsBoardActive())
        {
            return false;
        }
        if (!IsVisible)
        {
            return false;
        }
        if (!_presentationOptions.AllowOffice && !_presentationOptions.AllowWps)
        {
            return false;
        }
        var target = _presentationResolver.ResolvePresentationTarget(
            _presentationClassifier,
            _presentationOptions.AllowWps,
            _presentationOptions.AllowOffice,
            _currentProcessId);
        if (!target.IsValid)
        {
            return false;
        }
        if (!_presentationClassifier.IsSlideshowWindow(target.Info))
        {
            return false;
        }
        if (requireFullscreen && !IsFullscreenPresentationWindow(target))
        {
            return false;
        }
        var force = ShouldForcePresentationForeground(target);
        if (!force && !IsForegroundOwnedByCurrentProcess())
        {
            return false;
        }
        return ClassroomToolkit.Interop.Presentation.PresentationWindowFocus.EnsureForeground(target.Handle);
    }

    public void ForwardKeyboardToPresentation(Key key)
    {
        if (!_presentationOptions.AllowOffice && !_presentationOptions.AllowWps)
        {
            return;
        }
        // 将键盘按键转换为演示文稿命令
        ClassroomToolkit.Services.Presentation.PresentationCommand? command = null;
        if (key == Key.Right || key == Key.Down || key == Key.Space || key == Key.Enter || key == Key.PageDown)
        {
            command = ClassroomToolkit.Services.Presentation.PresentationCommand.Next;
        }
        else if (key == Key.Left || key == Key.Up || key == Key.PageUp)
        {
            command = ClassroomToolkit.Services.Presentation.PresentationCommand.Previous;
        }
        if (command == null)
        {
            return;
        }
        if (TrySendPresentationCommand(command.Value))
        {
        }
    }

    private void UpdatePresentationFocusMonitor()
    {
        var shouldMonitor = IsVisible
            && (_presentationOptions.AllowOffice
                || _presentationOptions.AllowWps
                || (_photoModeActive && _photoFullscreen));
        if (shouldMonitor)
        {
            if (!_presentationFocusMonitor.IsEnabled)
            {
                _presentationFocusMonitor.Start();
            }
            return;
        }
        if (_presentationFocusMonitor.IsEnabled)
        {
            _presentationFocusMonitor.Stop();
        }
    }

    private void DetectForegroundPresentation()
    {
        if (!_presentationOptions.AllowOffice && !_presentationOptions.AllowWps)
        {
            _foregroundPresentationActive = false;
            return;
        }
        var target = _presentationResolver.ResolveForeground();
        if (!target.IsValid || target.Info == null)
        {
            _foregroundPresentationActive = false;
            return;
        }
        var type = _presentationClassifier.Classify(target.Info);
        if (type == ClassroomToolkit.Interop.Presentation.PresentationType.None)
        {
            _foregroundPresentationActive = false;
            return;
        }
        if (!IsFullscreenPresentationWindow(target))
        {
            _foregroundPresentationActive = false;
            return;
        }
        if (_foregroundPresentationActive
            && _foregroundPresentationHandle == target.Handle
            && _foregroundPresentationType == type)
        {
            return;
        }
        _foregroundPresentationActive = true;
        _foregroundPresentationHandle = target.Handle;
        _foregroundPresentationType = type;
        PresentationForegroundDetected?.Invoke(type);
    }

    private void DetectForegroundPhoto()
    {
        if (!_photoModeActive || !_photoFullscreen)
        {
            _foregroundPhotoActive = false;
            return;
        }
        if (_hwnd == IntPtr.Zero)
        {
            _foregroundPhotoActive = false;
            return;
        }
        var foreground = Interop.NativeMethods.GetForegroundWindow();
        if (foreground != _hwnd)
        {
            _foregroundPhotoActive = false;
            return;
        }
        if (_foregroundPhotoActive)
        {
            return;
        }
        _foregroundPhotoActive = true;
        PhotoForegroundDetected?.Invoke();
    }

    private void MonitorPresentationFocus()
    {
        DetectForegroundPresentation();
        DetectForegroundPhoto();
        if (!_presentationFocusRestoreEnabled)
        {
            return;
        }
        if (_photoModeActive || IsBoardActive())
        {
            return;
        }
        if (DateTime.UtcNow < _nextPresentationFocusAttempt)
        {
            return;
        }
        if (!IsForegroundOwnedByCurrentProcess())
        {
            return;
        }
        var restored = RestorePresentationFocusIfNeeded(requireFullscreen: true);
        if (restored)
        {
            _nextPresentationFocusAttempt = DateTime.UtcNow.AddMilliseconds(PresentationFocusCooldownMs);
        }
    }

    private void MonitorInkContext()
    {
        var monitorStart = Stopwatch.StartNew();
        bool uiThread = Dispatcher.CheckAccess();
        _pendingInkContextCheck = false;

        var allowPresentation = _presentationOptions.AllowOffice || _presentationOptions.AllowWps;
        if (!allowPresentation)
        {
            if (_presentationFullscreenActive)
            {
                _presentationFullscreenActive = false;
                _currentPresentationType = ClassroomToolkit.Interop.Presentation.PresentationType.None;
                PresentationFullscreenDetected?.Invoke();
                if (!_photoModeActive && !IsBoardActive())
                {
                    _currentCacheScope = InkCacheScope.None;
                    _currentCacheKey = string.Empty;
                    ClearInkSurfaceState();
                }
            }
        }
        else
        {
            UpdatePresentationFullscreenState(clearInkOnExit: !_photoModeActive && !IsBoardActive());
        }

        if (_photoModeActive || IsBoardActive())
        {
            _perfMonitor.Add(monitorStart.Elapsed.TotalMilliseconds, uiThread);
            return;
        }
        if (ShouldDeferInkContext())
        {
            _pendingInkContextCheck = true;
            _perfMonitor.Add(monitorStart.Elapsed.TotalMilliseconds, uiThread);
            return;
        }

        UpdateInkMonitorInterval();
        _perfMonitor.Add(monitorStart.Elapsed.TotalMilliseconds, uiThread);
    }

    private void UpdatePresentationFullscreenState(bool clearInkOnExit)
    {
        var target = _presentationResolver.ResolvePresentationTarget(
            _presentationClassifier,
            _presentationOptions.AllowWps,
            _presentationOptions.AllowOffice,
            _currentProcessId);
        var fullscreenNow = target.IsValid && IsFullscreenPresentationWindow(target);
        var nextType = ClassroomToolkit.Interop.Presentation.PresentationType.None;
        if (fullscreenNow && target.Info != null)
        {
            nextType = _presentationClassifier.Classify(target.Info);
        }
        var stateChanged = fullscreenNow != _presentationFullscreenActive;
        _presentationFullscreenActive = fullscreenNow;
        _currentPresentationType = fullscreenNow ? nextType : ClassroomToolkit.Interop.Presentation.PresentationType.None;
        if (!stateChanged)
        {
            return;
        }
        PresentationFullscreenDetected?.Invoke();
        if (!fullscreenNow && clearInkOnExit)
        {
            _currentCacheScope = InkCacheScope.None;
            _currentCacheKey = string.Empty;
            ClearInkSurfaceState();
        }
    }

    private ClassroomToolkit.Interop.Presentation.PresentationType ResolveForegroundPresentationType()
    {
        var target = _presentationResolver.ResolveForeground();
        if (!target.IsValid || target.Info == null)
        {
            return ClassroomToolkit.Interop.Presentation.PresentationType.None;
        }
        return _presentationClassifier.Classify(target.Info);
    }

    public void UpdateWpsMode(string mode)
    {
        _presentationOptions.Strategy = mode switch
        {
            "raw" => ClassroomToolkit.Interop.Presentation.InputStrategy.Raw,
            "message" => ClassroomToolkit.Interop.Presentation.InputStrategy.Message,
            _ => ClassroomToolkit.Interop.Presentation.InputStrategy.Auto
        };
        _presentationService.ResetWpsAutoFallback();
        _presentationService.ResetOfficeAutoFallback();
        _wpsForceMessageFallback = false;
        _wpsHookUnavailableNotified = false;
        UpdateWpsNavHookState();
        UpdateFocusAcceptance();
    }

    public void UpdateWpsWheelMapping(bool enabled)
    {
        _presentationOptions.WheelAsKey = enabled;
        UpdateWpsNavHookState();
        UpdateFocusAcceptance();
    }

    public void UpdatePresentationTargets(bool allowOffice, bool allowWps)
    {
        _presentationOptions.AllowOffice = allowOffice;
        _presentationOptions.AllowWps = allowWps;
        if (!allowWps)
        {
            _wpsForceMessageFallback = false;
            _wpsHookUnavailableNotified = false;
        }
        _presentationService.ResetOfficeAutoFallback();
        UpdateWpsNavHookState();
        UpdateFocusAcceptance();
        UpdatePresentationFocusMonitor();
    }

    public void UpdatePresentationForegroundPolicy(bool forceForegroundOnFullscreen)
    {
        _forcePresentationForegroundOnFullscreen = forceForegroundOnFullscreen;
    }

    private void OnWpsNavHookRequested(int direction, string source)
    {
        if (!_presentationOptions.AllowWps)
        {
            return;
        }
        MarkWpsHookInput();
        if (IsBoardActive() || direction == 0)
        {
            return;
        }
        var target = ResolveWpsTarget();
        if (!target.IsValid)
        {
            return;
        }
        var passthrough = IsWpsRawInputPassthrough(target);
        var interceptSource = source == "wheel" ? _wpsHookInterceptWheel : _wpsHookInterceptKeyboard;
        if (passthrough && !interceptSource)
        {
            return;
        }
        if (ShouldSuppressWpsNav(direction, target.Handle))
        {
            return;
        }
        var command = direction > 0
            ? ClassroomToolkit.Services.Presentation.PresentationCommand.Next
            : ClassroomToolkit.Services.Presentation.PresentationCommand.Previous;
        var options = BuildWpsOptions(source);
        if (_presentationService.TrySendToTarget(target, command, options))
        {
            RememberWpsNav(direction, target.Handle);
        }
    }

    private bool TrySendWpsNavigation(ClassroomToolkit.Services.Presentation.PresentationCommand command)
    {
        if (!_presentationOptions.AllowWps)
        {
            return false;
        }
        if (IsBoardActive())
        {
            return false;
        }
        var target = ResolveWpsTarget();
        if (!target.IsValid)
        {
            return false;
        }
        return TrySendWpsNavigation(command, target, allowBackground: false);
    }

    private bool TrySendWpsNavigation(
        ClassroomToolkit.Services.Presentation.PresentationCommand command,
        ClassroomToolkit.Interop.Presentation.PresentationTarget target,
        bool allowBackground)
    {
        if (!_presentationOptions.AllowWps)
        {
            return false;
        }
        if (IsBoardActive())
        {
            return false;
        }
        if (!target.IsValid || target.Info == null)
        {
            return false;
        }
        if (!IsPresentationSlideshow(target))
        {
            return false;
        }
        if (!allowBackground && !IsTargetForeground(target))
        {
            return false;
        }
        var direction = command == ClassroomToolkit.Services.Presentation.PresentationCommand.Next ? 1 : -1;
        if (ShouldSuppressWpsNav(direction, target.Handle))
        {
            return false;
        }
        var options = BuildWpsOptions("wheel");
        var sent = _presentationService.TrySendToTarget(target, command, options);
        if (sent)
        {
            RememberWpsNav(direction, target.Handle);
        }
        return sent;
    }

    private bool TryHandlePresentationKey(Key key)
    {
        ClassroomToolkit.Services.Presentation.PresentationCommand? command = null;
        if (key == Key.Right || key == Key.Down || key == Key.Space || key == Key.Enter || key == Key.PageDown)
        {
            command = ClassroomToolkit.Services.Presentation.PresentationCommand.Next;
        }
        else if (key == Key.Left || key == Key.Up || key == Key.PageUp)
        {
            command = ClassroomToolkit.Services.Presentation.PresentationCommand.Previous;
        }
        if (command == null)
        {
            return false;
        }
        if (!TrySendPresentationCommand(command.Value))
        {
            return false;
        }
        return true;
    }

    private bool TrySendPresentationCommand(ClassroomToolkit.Services.Presentation.PresentationCommand command)
    {
        if (!_presentationOptions.AllowOffice && !_presentationOptions.AllowWps)
        {
            return false;
        }
        var wpsTarget = _presentationOptions.AllowWps
            ? ResolveWpsTarget()
            : ClassroomToolkit.Interop.Presentation.PresentationTarget.Empty;
        var officeTarget = _presentationOptions.AllowOffice
            ? _presentationResolver.ResolvePresentationTarget(
                _presentationClassifier,
                allowWps: false,
                allowOffice: true,
                _currentProcessId)
            : ClassroomToolkit.Interop.Presentation.PresentationTarget.Empty;
        var wpsSlideshow = IsPresentationSlideshow(wpsTarget);
        var officeSlideshow = IsPresentationSlideshow(officeTarget);
        var foreground = ResolveForegroundPresentationType();

        if (foreground == ClassroomToolkit.Interop.Presentation.PresentationType.Wps
            && wpsSlideshow
            && TrySendWpsNavigation(command, wpsTarget, allowBackground: false))
        {
            return true;
        }
        if (foreground == ClassroomToolkit.Interop.Presentation.PresentationType.Office
            && officeSlideshow
            && TrySendOfficeNavigation(command, officeTarget, allowBackground: false))
        {
            return true;
        }

        if (wpsSlideshow && officeSlideshow)
        {
            if (_currentPresentationType == ClassroomToolkit.Interop.Presentation.PresentationType.Wps
                && TrySendWpsNavigation(command, wpsTarget, allowBackground: true))
            {
                return true;
            }
            if (_currentPresentationType == ClassroomToolkit.Interop.Presentation.PresentationType.Office
                && TrySendOfficeNavigation(command, officeTarget, allowBackground: true))
            {
                return true;
            }
            var wpsFullscreen = IsFullscreenWindow(wpsTarget.Handle);
            var officeFullscreen = IsFullscreenWindow(officeTarget.Handle);
            if (wpsFullscreen && !officeFullscreen
                && TrySendWpsNavigation(command, wpsTarget, allowBackground: true))
            {
                return true;
            }
            if (officeFullscreen && !wpsFullscreen
                && TrySendOfficeNavigation(command, officeTarget, allowBackground: true))
            {
                return true;
            }
        }

        if (wpsSlideshow && TrySendWpsNavigation(command, wpsTarget, allowBackground: true))
        {
            return true;
        }
        if (officeSlideshow && TrySendOfficeNavigation(command, officeTarget, allowBackground: true))
        {
            return true;
        }
        return false;
    }

    private bool TrySendOfficeNavigation(
        ClassroomToolkit.Services.Presentation.PresentationCommand command,
        ClassroomToolkit.Interop.Presentation.PresentationTarget target,
        bool allowBackground)
    {
        if (!_presentationOptions.AllowOffice)
        {
            return false;
        }
        if (IsBoardActive())
        {
            return false;
        }
        if (!target.IsValid || target.Info == null)
        {
            return false;
        }
        if (!IsPresentationSlideshow(target))
        {
            return false;
        }
        if (!allowBackground && !IsTargetForeground(target))
        {
            return false;
        }
        var options = new ClassroomToolkit.Services.Presentation.PresentationControlOptions
        {
            Strategy = _presentationOptions.Strategy,
            WheelAsKey = _presentationOptions.WheelAsKey,
            AllowOffice = true,
            AllowWps = false
        };
        return _presentationService.TrySendToTarget(target, command, options);
    }

    private bool IsPresentationSlideshow(ClassroomToolkit.Interop.Presentation.PresentationTarget target)
    {
        if (!target.IsValid || target.Info == null)
        {
            return false;
        }
        if (_presentationClassifier.IsSlideshowWindow(target.Info))
        {
            return true;
        }
        return IsFullscreenWindow(target.Handle);
    }

    private ClassroomToolkit.Interop.Presentation.PresentationType ResolvePreferredPresentationType()
    {
        var foreground = ResolveForegroundPresentationType();
        if (foreground == ClassroomToolkit.Interop.Presentation.PresentationType.Office
            || foreground == ClassroomToolkit.Interop.Presentation.PresentationType.Wps)
        {
            return foreground;
        }
        var fullscreen = ResolveFullscreenPresentationType();
        if (fullscreen != ClassroomToolkit.Interop.Presentation.PresentationType.None)
        {
            return fullscreen;
        }
        return _currentPresentationType;
    }

    private ClassroomToolkit.Interop.Presentation.PresentationType ResolveFullscreenPresentationType()
    {
        bool wpsFullscreen = false;
        bool officeFullscreen = false;
        if (_presentationOptions.AllowWps)
        {
            var wpsTarget = ResolveWpsTarget();
            wpsFullscreen = IsFullscreenPresentationWindow(wpsTarget);
        }
        if (_presentationOptions.AllowOffice)
        {
            var officeTarget = _presentationResolver.ResolvePresentationTarget(
                _presentationClassifier,
                allowWps: false,
                allowOffice: true,
                _currentProcessId);
            officeFullscreen = IsFullscreenPresentationWindow(officeTarget);
        }
        if (wpsFullscreen && !officeFullscreen)
        {
            return ClassroomToolkit.Interop.Presentation.PresentationType.Wps;
        }
        if (officeFullscreen && !wpsFullscreen)
        {
            return ClassroomToolkit.Interop.Presentation.PresentationType.Office;
        }
        if (wpsFullscreen && officeFullscreen
            && _currentPresentationType != ClassroomToolkit.Interop.Presentation.PresentationType.None)
        {
            return _currentPresentationType;
        }
        return ClassroomToolkit.Interop.Presentation.PresentationType.None;
    }

    private ClassroomToolkit.Services.Presentation.PresentationControlOptions BuildWpsOptions(string? source = null)
    {
        var strategy = _presentationOptions.Strategy;
        if (_wpsForceMessageFallback)
        {
            strategy = ClassroomToolkit.Interop.Presentation.InputStrategy.Message;
        }
        if (string.Equals(source, "wheel", StringComparison.OrdinalIgnoreCase) && _presentationOptions.WheelAsKey)
        {
            strategy = ClassroomToolkit.Interop.Presentation.InputStrategy.Message;
        }
        return new ClassroomToolkit.Services.Presentation.PresentationControlOptions
        {
            Strategy = strategy,
            WheelAsKey = _presentationOptions.WheelAsKey,
            AllowOffice = false,
            AllowWps = true
        };
    }

    private void UpdateWpsNavHookState()
    {
        if (_wpsNavHook == null || !_wpsNavHook.Available)
        {
            _wpsNavHookActive = false;
            if (_presentationOptions.AllowWps)
            {
                var hookTarget = ResolveWpsTarget();
                MarkWpsHookUnavailable(hookTarget.IsValid);
            }
            return;
        }
        _wpsForceMessageFallback = false;
        var shouldEnable = _presentationOptions.AllowWps && !IsBoardActive() && IsVisible && !_photoModeActive;
        var blockOnly = false;
        var interceptKeyboard = true;
        var interceptWheel = true;
        var emitWheelOnBlock = true;
        var target = ClassroomToolkit.Interop.Presentation.PresentationTarget.Empty;
        if (shouldEnable)
        {
            target = ResolveWpsTarget();
            shouldEnable = target.IsValid;
        }
        var sendMode = ClassroomToolkit.Interop.Presentation.InputStrategy.Message;
        var wheelForward = false;
        if (shouldEnable)
        {
            sendMode = ResolveWpsSendMode(target);
            wheelForward = _presentationOptions.WheelAsKey;
            interceptWheel = wheelForward;
            emitWheelOnBlock = wheelForward;
        }
        
        // 光标模式下，直接禁用钩子拦截，让输入直接传递到 WPS
        if (_mode == PaintToolMode.Cursor)
        {
            interceptKeyboard = false;
            interceptWheel = false;
        }
        else if (shouldEnable && !IsTargetForeground(target))
        {
            interceptKeyboard = false;
            interceptWheel = false;
            emitWheelOnBlock = false;
        }
        else if (shouldEnable && sendMode == ClassroomToolkit.Interop.Presentation.InputStrategy.Raw)
        {
            blockOnly = true;
            if (IsTargetForeground(target))
            {
                if (!wheelForward)
                {
                    blockOnly = false;
                    emitWheelOnBlock = false;
                }
            }
        }
        
        if (shouldEnable)
        {
            _wpsNavHook.SetInterceptEnabled(true);
            _wpsNavHook.SetBlockOnly(blockOnly);
            _wpsNavHook.SetInterceptKeyboard(interceptKeyboard);
            _wpsNavHook.SetInterceptWheel(interceptWheel);
            _wpsNavHook.SetEmitWheelOnBlock(emitWheelOnBlock);
            _wpsHookInterceptKeyboard = interceptKeyboard;
            _wpsHookInterceptWheel = interceptWheel;
            if (!_wpsNavHookActive)
            {
                _wpsNavHookActive = _wpsNavHook.Start();
            }
            if (!_wpsNavHookActive)
            {
                StopWpsNavHook();
                MarkWpsHookUnavailable(target.IsValid);
            }
            else
            {
                _wpsForceMessageFallback = false;
            }
            return;
        }
        StopWpsNavHook();
    }

    private void StopWpsNavHook()
    {
        if (_wpsNavHook == null)
        {
            return;
        }
        _wpsNavHook.SetInterceptEnabled(false);
        _wpsNavHook.SetBlockOnly(false);
        _wpsNavHook.SetInterceptKeyboard(true);
        _wpsNavHook.SetInterceptWheel(true);
        _wpsNavHook.SetEmitWheelOnBlock(true);
        _wpsNavHook.Stop();
        _wpsNavHookActive = false;
        _wpsHookInterceptKeyboard = true;
        _wpsHookInterceptWheel = true;
    }

    private ClassroomToolkit.Interop.Presentation.PresentationTarget ResolveWpsTarget()
    {
        return _presentationResolver.ResolvePresentationTarget(
            _presentationClassifier,
            allowWps: true,
            allowOffice: false,
            (uint)Environment.ProcessId);
    }

    private ClassroomToolkit.Interop.Presentation.InputStrategy ResolveWpsSendMode(
        ClassroomToolkit.Interop.Presentation.PresentationTarget target)
    {
        if (_wpsForceMessageFallback)
        {
            return ClassroomToolkit.Interop.Presentation.InputStrategy.Message;
        }
        var mode = _presentationOptions.Strategy;
        if (mode == ClassroomToolkit.Interop.Presentation.InputStrategy.Auto)
        {
            if (_presentationService.IsWpsAutoForcedMessage)
            {
                return ClassroomToolkit.Interop.Presentation.InputStrategy.Message;
            }
            return target.IsValid
                ? ClassroomToolkit.Interop.Presentation.InputStrategy.Raw
                : ClassroomToolkit.Interop.Presentation.InputStrategy.Message;
        }
        return mode;
    }

    private void MarkWpsHookUnavailable(bool notify)
    {
        _wpsForceMessageFallback = true;
        if (notify)
        {
            NotifyWpsHookUnavailable();
        }
    }

    private void NotifyWpsHookUnavailable()
    {
        if (_wpsHookUnavailableNotified)
        {
            return;
        }
        _wpsHookUnavailableNotified = true;
        Dispatcher.BeginInvoke(() =>
        {
            var owner = System.Windows.Application.Current?.MainWindow;
            var message = "检测到 WPS 放映全局钩子不可用，已自动切换为消息投递模式。";
            System.Windows.MessageBox.Show(owner ?? this, message, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        });
    }

    private bool IsWpsRawInputPassthrough(ClassroomToolkit.Interop.Presentation.PresentationTarget target)
    {
        if (ResolveWpsSendMode(target) != ClassroomToolkit.Interop.Presentation.InputStrategy.Raw)
        {
            return false;
        }
        return IsTargetForeground(target);
    }

    private bool IsTargetForeground(ClassroomToolkit.Interop.Presentation.PresentationTarget target)
    {
        if (!target.IsValid)
        {
            return false;
        }
        return ClassroomToolkit.Interop.Presentation.PresentationWindowFocus.IsForeground(target.Handle);
    }

    private bool ShouldSuppressWpsNav(int direction, IntPtr target)
    {
        if (target == IntPtr.Zero)
        {
            return false;
        }
        if (_wpsNavBlockUntil > DateTime.UtcNow)
        {
            return true;
        }
        if (_lastWpsNavEvent.HasValue)
        {
            var last = _lastWpsNavEvent.Value;
            if (last.Code == direction && last.Target == target)
            {
                var elapsed = DateTime.UtcNow - last.Timestamp;
                if (elapsed.TotalMilliseconds < WpsNavDebounceMs)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private void RememberWpsNav(int direction, IntPtr target)
    {
        _lastWpsNavEvent = (direction, target, DateTime.UtcNow);
        _wpsNavBlockUntil = DateTime.UtcNow.AddMilliseconds(WpsNavDebounceMs);
    }

    private void MarkWpsHookInput()
    {
        _lastWpsHookInput = DateTime.UtcNow;
    }

    private bool WpsHookRecentlyFired()
    {
        if (_lastWpsHookInput == DateTime.MinValue)
        {
            return false;
        }
        return (DateTime.UtcNow - _lastWpsHookInput).TotalMilliseconds < WpsNavDebounceMs;
    }

    private bool IsForegroundOwnedByCurrentProcess()
    {
        var foreground = Interop.NativeMethods.GetForegroundWindow();
        if (foreground == IntPtr.Zero)
        {
            return false;
        }
        Interop.NativeMethods.GetWindowThreadProcessId(foreground, out var processId);
        return processId == (uint)Environment.ProcessId;
    }

    private bool ShouldForcePresentationForeground(ClassroomToolkit.Interop.Presentation.PresentationTarget target)
    {
        if (!_forcePresentationForegroundOnFullscreen)
        {
            return false;
        }
        // 特殊逻辑：仅当前进程正在主导交互时才强制抢占
        return IsFullscreenWindow(target.Handle);
    }

    private bool IsFullscreenPresentationWindow(ClassroomToolkit.Interop.Presentation.PresentationTarget target)
    {
        if (!target.IsValid || target.Info == null)
        {
            return false;
        }
        if (!_presentationClassifier.IsSlideshowWindow(target.Info))
        {
            return false;
        }
        return IsFullscreenWindow(target.Handle);
    }

    private bool IsFullscreenWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }
        var monitor = Interop.NativeMethods.MonitorFromWindow(hwnd, Interop.NativeMethods.MonitorDefaultToNearest);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!Interop.NativeMethods.GetMonitorInfo(monitor, ref info))
        {
            return false;
        }
        var rect = new NativeRect();
        if (!Interop.NativeMethods.GetWindowRect(hwnd, ref rect))
        {
            return false;
        }
        const int tolerance = 2;
        return Math.Abs(rect.Left - info.Monitor.Left) <= tolerance
               && Math.Abs(rect.Top - info.Monitor.Top) <= tolerance
               && Math.Abs(rect.Right - info.Monitor.Right) <= tolerance
               && Math.Abs(rect.Bottom - info.Monitor.Bottom) <= tolerance;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }
}
