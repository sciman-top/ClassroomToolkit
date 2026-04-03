using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ClassroomToolkit.App.Diagnostics;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.Settings;
using ClassroomToolkit.App.Utilities;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App;

/// <summary>
/// Launcher UI: minimize/restore, bubble window, auto-exit, diagnostics, drag, about dialog,
/// and window position utilities.
/// </summary>
public partial class MainWindow
{
    private void OnMinimizeClick(object sender, RoutedEventArgs e)
    {
        MinimizeLauncher(fromSettings: false);
    }

    private void OnAboutClick(object sender, RoutedEventArgs e)
    {
        var dialog = new AboutDialog
        {
            Owner = this
        };
        _ = TryShowDialogWithDiagnostics(dialog, nameof(AboutDialog));
    }

    private void OnDiagnosticsClick(object sender, RoutedEventArgs e)
    {
        var settingsPath = string.IsNullOrWhiteSpace(_configurationService.SettingsDocumentPath)
            ? _configurationService.SettingsIniPath
            : _configurationService.SettingsDocumentPath;
        var studentPath = ResolveStudentWorkbookPath();
        var photoRoot = _settings.InkPhotoRootPath;
        var result = SystemDiagnostics.CollectSystemDiagnostics(
            _settings,
            settingsPath,
            studentPath,
            photoRoot);
        var dialog = new DiagnosticsDialog(result, _settingsService, _settings)
        {
            Owner = this
        };
        _ = TryShowDialogWithDiagnostics(dialog, nameof(DiagnosticsDialog));
    }

    private void OnLauncherSettingsClick(object sender, RoutedEventArgs e)
    {
        var currentMinutes = Math.Max(0, _settings.LauncherAutoExitSeconds / MainWindowRuntimeDefaults.LauncherMinutesToSeconds);
        var dialog = new AutoExitDialog(currentMinutes)
        {
            Owner = this
        };
        
        TryFixWindowBorders(this, "launcher-settings", "main-window");
        TryFixWindowBorders(dialog, "launcher-settings", "auto-exit-dialog");

        if (!TryShowDialogWithDiagnostics(dialog, nameof(AutoExitDialog)))
        {
            return;
        }
        _settings.LauncherAutoExitSeconds = Math.Max(0, dialog.Minutes) * MainWindowRuntimeDefaults.LauncherMinutesToSeconds;
        ScheduleAutoExitTimer();
        SaveLauncherSettings();
    }

    private void OnExitClick(object sender, RoutedEventArgs e)
    {
        RequestExit();
    }

    private void OnDragAreaMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }
        if (IsDragBlocked(e.OriginalSource))
        {
            return;
        }
        if (!this.SafeDragMove(ex =>
        {
            System.Diagnostics.Debug.WriteLine(
                LauncherDragDiagnosticsPolicy.FormatDragMoveFailureMessage(
                    ex.GetType().Name,
                    ex.Message));
        }))
        {
            return;
        }
        EnsureWithinWorkArea();
        SaveLauncherSettings();
    }

    private bool IsDragBlocked(object? source)
    {
        return FindAncestor<System.Windows.Controls.Primitives.ButtonBase>(source as DependencyObject) != null;
    }

    private static T? FindAncestor<T>(DependencyObject? obj) where T : DependencyObject
    {
        while (obj != null)
        {
            if (obj is T match)
            {
                return match;
            }
            obj = GetParent(obj);
        }
        return null;
    }

    private static DependencyObject? GetParent(DependencyObject obj)
    {
        if (obj is System.Windows.Documents.TextElement textElement)
        {
            return textElement.Parent;
        }
        if (obj is FrameworkContentElement contentElement)
        {
            return contentElement.Parent;
        }
        var parent = VisualTreeHelper.GetParent(obj);
        if (parent == null && obj is FrameworkElement element)
        {
            parent = element.Parent as DependencyObject;
        }
        return parent ?? LogicalTreeHelper.GetParent(obj);
    }

    private static string ResolveStudentWorkbookPath()
    {
        return StudentResourceLocator.ResolveStudentWorkbookPath();
    }

    private void ApplyLauncherPosition()
    {
        if (_settings.LauncherX == AppSettings.UnsetPosition
            || _settings.LauncherY == AppSettings.UnsetPosition)
        {
            WindowPlacementHelper.CenterOnVirtualScreen(this);
            EnsureWithinWorkArea();
            return;
        }

        Left = _settings.LauncherX;
        Top = _settings.LauncherY;
        EnsureWithinWorkArea();
    }

    private void EnsureWithinWorkArea()
    {
        var resolvedPosition = LauncherWorkAreaClampPolicy.Resolve(
            Left,
            Top,
            Width,
            Height,
            SystemParameters.WorkArea);
        Left = resolvedPosition.X;
        Top = resolvedPosition.Y;
    }

    private void EnsureBubbleWindow()
    {
        if (_bubbleWindow != null)
        {
            return;
        }
        _bubbleWindow = new LauncherBubbleWindow();
        WireBubbleWindow(_bubbleWindow);
    }

    private void WireBubbleWindow(LauncherBubbleWindow bubbleWindow)
    {
        bubbleWindow.RestoreRequested += RestoreLauncher;
        bubbleWindow.PositionChanged += OnBubblePositionChanged;
        bubbleWindow.IsVisibleChanged += OnBubbleWindowVisibleChanged;
    }

    private void OnBubbleWindowVisibleChanged(object? sender, DependencyPropertyChangedEventArgs e)
    {
        var currentVisibleState = _bubbleWindow?.IsVisible == true;
        var nowUtc = GetCurrentUtcTimestamp();
        var gateDecision = LauncherBubbleZOrderApplyGatePolicy.Resolve(
            currentVisibleState,
            _bubbleVisibilityState.SuppressVisibleChangedApply,
            _bubbleVisibilityState.SuppressVisibleChangedUntilUtc,
            nowUtc,
            _allowClose,
            _bubbleWindow != null);
        if (!gateDecision.ShouldApply)
        {
            System.Diagnostics.Debug.WriteLine(
                LauncherBubbleDiagnosticsPolicy.FormatVisibleChangedGateSkipMessage(
                    gateDecision.Reason,
                    gateDecision.VisibleChangedReason));
            return;
        }

        var interactionState = CaptureOverlayInteractionState();
        var dedupIntervalMs = LauncherBubbleVisibleChangedDedupIntervalPolicy.ResolveMs(
            overlayVisible: interactionState.OverlayVisible,
            photoModeActive: interactionState.PhotoModeActive,
            whiteboardActive: interactionState.WhiteboardActive);
        var dedupDecision = LauncherBubbleVisibleChangedDedupPolicy.Resolve(
            currentVisibleState,
            _bubbleVisibilityState.VisibleChangedState,
            nowUtc,
            minIntervalMs: dedupIntervalMs);
        LauncherBubbleVisibilityStateUpdater.ApplyVisibleChangedDecision(
            ref _bubbleVisibilityState,
            dedupDecision);
        if (!dedupDecision.ShouldApply)
        {
            System.Diagnostics.Debug.WriteLine(
                LauncherBubbleDiagnosticsPolicy.FormatVisibleChangedDedupSkipMessage(
                    dedupDecision.Reason));
            return;
        }

        var decision = LauncherBubbleVisibilityPolicy.Resolve(
            bubbleVisible: currentVisibleState);
        FloatingZOrderApplyExecutor.Apply(
            decision.RequestZOrderApply,
            decision.ForceEnforceZOrder,
            RequestApplyZOrderPolicy);
    }

    private void MinimizeLauncher(bool fromSettings)
    {
        EnsureBubbleWindow();
        if (_bubbleWindow == null)
        {
            return;
        }

        var minimizeDecision = LauncherVisibilityTransitionPolicy.ResolveMinimizeDecision(
            CaptureLauncherMinimizeTransitionContext(_bubbleWindow));
        var transitionPlan = minimizeDecision.Plan;
        var hasSavedBubblePosition = _settings.LauncherBubbleX != AppSettings.UnsetPosition
            && _settings.LauncherBubbleY != AppSettings.UnsetPosition;
        var target = fromSettings
            ? (hasSavedBubblePosition
                ? new System.Windows.Point(_settings.LauncherBubbleX, _settings.LauncherBubbleY)
                : new System.Windows.Point(Left + Width / 2, Top + Height / 2))
            : new System.Windows.Point(Left + Width / 2, Top + Height / 2);
        _bubbleWindow.PlaceNear(target);
        System.Diagnostics.Debug.WriteLine(
            $"[Launcher][Minimize] reason={LauncherVisibilityTransitionReasonPolicy.ResolveMinimizeTag(minimizeDecision.Reason)}");
        _settings.LauncherMinimized = true;
        ApplyLauncherMinimizeTransition(transitionPlan);
    }

    private void RestoreLauncher()
    {
        var restoreDecision = LauncherVisibilityTransitionPolicy.ResolveRestoreDecision(
            CaptureLauncherRestoreTransitionContext());
        var transitionPlan = restoreDecision.Plan;
        System.Diagnostics.Debug.WriteLine(
            $"[Launcher][Restore] reason={LauncherVisibilityTransitionReasonPolicy.ResolveRestoreTag(restoreDecision.Reason)}");
        _settings.LauncherMinimized = false;
        ApplyLauncherRestoreTransition(transitionPlan);
    }

    private LauncherMinimizeTransitionContext CaptureLauncherMinimizeTransitionContext(
        LauncherBubbleWindow bubbleWindow)
    {
        return new LauncherMinimizeTransitionContext(
            MainVisible: IsVisible,
            BubbleVisible: bubbleWindow.IsVisible);
    }

    private LauncherRestoreTransitionContext CaptureLauncherRestoreTransitionContext()
    {
        return new LauncherRestoreTransitionContext(
            MainVisible: IsVisible,
            MainActive: IsActive,
            BubbleVisible: _bubbleWindow?.IsVisible == true);
    }

    private void ApplyLauncherMinimizeTransition(LauncherVisibilityTransitionPlan transitionPlan)
    {
        if (transitionPlan.ShowBubbleWindow)
        {
            ExecuteBubbleVisibilityTransition(() => _bubbleWindow?.Show());
        }
        if (transitionPlan.HideMainWindow)
        {
            ExecuteLifecycleSafe("launcher-transition", "hide-main-window", Hide);
        }
        SaveLauncherSettings();
        FloatingZOrderApplyExecutor.Apply(
            transitionPlan.RequestZOrderApply,
            transitionPlan.ForceEnforceZOrder,
            RequestApplyZOrderPolicy);
    }

    private void ApplyLauncherRestoreTransition(LauncherVisibilityTransitionPlan transitionPlan)
    {
        if (transitionPlan.HideBubbleWindow)
        {
            ExecuteBubbleVisibilityTransition(() => _bubbleWindow?.Hide());
        }
        if (transitionPlan.ShowMainWindow)
        {
            ExecuteLifecycleSafe("launcher-transition", "show-main-window", Show);
        }
        UserInitiatedWindowExecutionExecutor.Apply(
            this,
            transitionPlan.ActivateMainWindow);
        EnsureWithinWorkArea();
        SaveLauncherSettings();
        UpdateToggleButtons();
        FloatingZOrderApplyExecutor.Apply(
            transitionPlan.RequestZOrderApply,
            transitionPlan.ForceEnforceZOrder,
            RequestApplyZOrderPolicy);
    }

    private void ExecuteBubbleVisibilityTransition(Action transition)
    {
        LauncherBubbleVisibilityStateUpdater.MarkSuppressVisibleChangedApply(
            ref _bubbleVisibilityState,
            suppress: true);
        try
        {
            ExecuteLifecycleSafe("launcher-bubble-transition", "apply-bubble-visibility", transition);
        }
        finally
        {
            LauncherBubbleVisibilityStateUpdater.MarkSuppressVisibleChangedApply(
                ref _bubbleVisibilityState,
                suppress: false);
            var interactionState = CaptureOverlayInteractionState();
            var cooldownMs = LauncherBubbleVisibleChangedSuppressionPolicy.ResolveCooldownMs(
                overlayVisible: interactionState.OverlayVisible,
                photoModeActive: interactionState.PhotoModeActive,
                whiteboardActive: interactionState.WhiteboardActive);
            LauncherBubbleVisibilityStateUpdater.MarkVisibleChangedSuppressionCooldown(
                ref _bubbleVisibilityState,
                GetCurrentUtcTimestamp(),
                cooldownMs: cooldownMs);
        }
    }

    private void OnBubblePositionChanged(System.Windows.Point position)
    {
        _settings.LauncherBubbleX = (int)Math.Round(position.X);
        _settings.LauncherBubbleY = (int)Math.Round(position.Y);
        if (_settings.LauncherMinimized)
        {
            SaveLauncherSettings();
        }
    }

    private void ScheduleAutoExitTimer()
    {
        _autoExitTimer.Stop();
        var timerPlan = LauncherAutoExitTimerPlanPolicy.Resolve(_settings.LauncherAutoExitSeconds);
        if (timerPlan.ShouldStart)
        {
            _autoExitTimer.Interval = timerPlan.Interval;
            _autoExitTimer.Start();
        }
    }

    private void RunStartupDiagnostics()
    {
        var startupWarningAlreadyShown =
            System.Windows.Application.Current.Properties.Contains(App.StartupCompatibilityWarningShownPropertyKey);
        if (!StartupDiagnosticsGatePolicy.ShouldRun(Environment.GetEnvironmentVariable("CTOOL_NO_STARTUP_DIAG")))
        {
            return;
        }
        if (startupWarningAlreadyShown)
        {
            return;
        }
        var settingsPath = string.IsNullOrWhiteSpace(_configurationService.SettingsDocumentPath)
            ? _configurationService.SettingsIniPath
            : _configurationService.SettingsDocumentPath;
        var studentPath = ResolveStudentWorkbookPath();
        var photoRoot = _settings.InkPhotoRootPath;
        _ = SafeTaskRunner.Run("MainWindow.StartupDiagnostics", async token =>
        {
            var result = SystemDiagnostics.CollectSystemDiagnostics(
                _settings,
                settingsPath,
                studentPath,
                photoRoot);
            if (!result.HasIssues)
            {
                return;
            }
            if (_backgroundTasksCancellation.IsCancellationRequested || token.IsCancellationRequested)
            {
                return;
            }
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
            {
                return;
            }

            await Task.Delay(
                MainWindowRuntimeDefaults.StartupDiagnosticsDialogDelayMs,
                token).ConfigureAwait(false);

            void ShowDiagnosticsDialog()
            {
                if (_backgroundTasksCancellation.IsCancellationRequested || token.IsCancellationRequested)
                {
                    return;
                }
                if (!IsLoaded || !IsVisible || WindowState == WindowState.Minimized)
                {
                    return;
                }
                var dialog = new DiagnosticsDialog(result, _settingsService, _settings)
                {
                    Owner = this
                };
                TryShowDialogWithDiagnostics(dialog, nameof(DiagnosticsDialog));
            }

            var scheduled = false;
            SafeActionExecutionExecutor.TryExecute(
                () =>
                {
                    _ = Dispatcher.InvokeAsync(ShowDiagnosticsDialog);
                    scheduled = true;
                },
                ex => System.Diagnostics.Debug.WriteLine(
                    $"MainWindow: startup diagnostics dispatch failed: {ex.GetType().Name} - {ex.Message}"));
            if (!scheduled && Dispatcher.CheckAccess())
            {
                ShowDiagnosticsDialog();
            }
        }, _backgroundTasksCancellation.Token, ex =>
        {
            System.Diagnostics.Debug.WriteLine($"MainWindow: startup diagnostics failed: {ex.GetType().Name} - {ex.Message}");
        });
    }

    private void SaveLauncherSettings()
    {
        _settings.LauncherX = (int)Math.Round(Left);
        _settings.LauncherY = (int)Math.Round(Top);
        if (_bubbleWindow != null)
        {
            _settings.LauncherBubbleX = (int)Math.Round(_bubbleWindow.Left);
            _settings.LauncherBubbleY = (int)Math.Round(_bubbleWindow.Top);
        }
        SaveSettings();
    }
}
