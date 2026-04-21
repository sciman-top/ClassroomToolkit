using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Utilities;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App;

public partial class MainWindow
{
    private void OnAutoExitTimerTick(object? sender, EventArgs e)
    {
        if (ShouldIgnoreShutdownTicks())
        {
            _autoExitTimer.Stop();
            return;
        }

        _autoExitTimer.Stop();
        RequestExit();
    }

    private void OnPresentationForegroundSuppressionTimerTick(object? sender, EventArgs e)
    {
        if (ShouldIgnoreShutdownTicks())
        {
            _presentationForegroundSuppressionTimer.Stop();
            return;
        }

        ReleasePresentationForegroundSuppression();
    }

    private void OnFloatingTopmostWatchdogTick(object? sender, EventArgs e)
    {
        if (ShouldIgnoreShutdownTicks())
        {
            _floatingTopmostWatchdogTimer.Stop();
            return;
        }

        if (FloatingTopmostDialogSuppressionState.IsSuppressed)
        {
            return;
        }

        var launcherVisible = LauncherVisibilityPolicy.IsVisibleForTopmost(
            launcherMinimized: _settings.LauncherMinimized,
            mainVisible: IsVisible,
            mainMinimized: WindowState == WindowState.Minimized,
            bubbleVisible: _bubbleWindow?.IsVisible == true,
            bubbleMinimized: _bubbleWindow?.WindowState == WindowState.Minimized);
        var shouldRetouch = FloatingTopmostWatchdogPolicy.ShouldForceRetouch(
            toolbarVisible: _toolbarWindow?.IsVisible == true,
            rollCallVisible: _rollCallWindow?.IsVisible == true,
            launcherVisible: launcherVisible,
            imageManagerVisible: _imageManagerWindow?.IsVisible == true,
            rollCallAuxOverlayVisible: _rollCallWindow?.HasVisibleAuxOverlay() == true,
            photoModeActive: _overlayWindow?.IsPhotoModeActive == true);
        if (!shouldRetouch)
        {
            return;
        }

        RequestApplyZOrderPolicy(forceEnforceZOrder: true);
    }

    private void OnMainWindowVisibleChanged(object? sender, DependencyPropertyChangedEventArgs e)
    {
        if (MainWindowVisibleChangedPolicy.ShouldEnsureVisible(IsVisible))
        {
            WindowPlacementHelper.EnsureVisible(this);
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyLauncherPosition();
        WindowPlacementHelper.EnsureVisible(this);
        ScheduleAutoExitTimer();
        _floatingTopmostWatchdogTimer.Start();
        ScheduleStartupWarmups();
        var toggleAction = MainWindowLoadedToggleActionPolicy.Resolve(_settings.LauncherMinimized);
        if (toggleAction == MainWindowLoadedToggleAction.MinimizeLauncher)
        {
            MinimizeLauncher(fromSettings: true);
        }
        else
        {
            UpdateToggleButtons();
        }

        RunStartupDiagnostics();
    }

    private void WarmupRollCallData()
    {
        EnsureRollCallWindow();
        _rollCallWindow?.WarmupData();
    }

    private void ScheduleStartupWarmups()
    {
        QueueStartupWarmup(
            operation: "warmup-rollcall-data",
            action: WarmupRollCallData,
            priority: DispatcherPriority.ContextIdle);
        QueueStartupWarmup(
            operation: "schedule-ink-cleanup",
            action: ScheduleInkCleanup,
            priority: DispatcherPriority.Background);
    }

    private void QueueStartupWarmup(string operation, Action action, DispatcherPriority priority)
    {
        if (_backgroundTasksCancellation.IsCancellationRequested)
        {
            return;
        }

        void ExecuteWarmup()
        {
            if (_backgroundTasksCancellation.IsCancellationRequested)
            {
                return;
            }

            ExecuteLifecycleSafe("startup-warmup", operation, action);
        }

        var scheduled = TryBeginInvoke(
            ExecuteWarmup,
            priority,
            $"StartupWarmup.{operation}");
        if (!scheduled && Dispatcher.CheckAccess())
        {
            ExecuteWarmup();
        }
    }

    private void RequestExit()
    {
        var exitPlan = MainWindowExitPlanPolicy.Resolve(
            allowClose: _allowClose,
            backgroundTasksCancellationRequested: _backgroundTasksCancellation.IsCancellationRequested,
            hasBubbleWindow: _bubbleWindow != null,
            hasRollCallWindow: _rollCallWindow != null,
            hasImageManagerWindow: _imageManagerWindow != null);
        if (!exitPlan.ShouldExit)
        {
            return;
        }

        _allowClose = true;
        const string phase = "request-exit";

        if (exitPlan.ShouldCancelBackgroundTasks)
        {
            ExecuteLifecycleSafe(phase, "cancel-background-tasks", () => _backgroundTasksCancellation.Cancel());
        }

        ExecuteLifecycleSafe(phase, "reset-toolbar-retouch-runtime", () => ResetToolbarInteractionRetouchRuntime(ToolbarInteractionRetouchRuntimeResetReason.RequestExit));
        ExecuteLifecycleSafe(phase, "trigger-ink-cleanup", TriggerInkCleanup);
        ExecuteLifecycleSafe(phase, "capture-toolbar-position", () => CapturePaintToolbarPosition(save: true));
        ExecuteLifecycleSafe(phase, "save-launcher-settings", SaveLauncherSettings);
        if (exitPlan.ShouldCloseBubbleWindow && _bubbleWindow != null)
        {
            var bubbleWindow = _bubbleWindow;
            ExecuteLifecycleSafe(phase, "close-launcher-bubble-window", bubbleWindow.Close);
            _bubbleWindow = null;
        }

        if (exitPlan.ShouldCloseRollCallWindow && _rollCallWindow != null)
        {
            var rollCallWindow = _rollCallWindow;
            ExecuteLifecycleSafe(phase, "close-rollcall-window", rollCallWindow.RequestClose);
            _rollCallWindow = null;
        }

        if (exitPlan.ShouldCloseImageManagerWindow && _imageManagerWindow != null)
        {
            var imageManagerWindow = _imageManagerWindow;
            ExecuteLifecycleSafe(phase, "close-image-manager-window", imageManagerWindow.Close);
        }

        ExecuteLifecycleSafe(phase, "close-paint-window-orchestrator", _paintWindowOrchestrator.Close);
        ExecuteLifecycleSafe(phase, "shutdown-application", () => System.Windows.Application.Current.Shutdown());
    }

    private bool ShouldIgnoreShutdownTicks()
    {
        return _backgroundTasksCancellationDisposed || _backgroundTasksCancellation.IsCancellationRequested;
    }

    private void ExecuteLifecycleSafe(string phase, string operation, Action action)
    {
        SafeActionExecutionExecutor.TryExecute(
            action,
            ex => System.Diagnostics.Debug.WriteLine(
                LifecycleSafeExecutionDiagnosticsPolicy.FormatFailureMessage(
                    phase,
                    operation,
                    ex.GetType().Name,
                    ex.Message)));
    }

    private void TryFixWindowBorders(Window window, string phase, string target)
    {
        ExecuteLifecycleSafe(
            phase,
            $"border-fix-{target}",
            () =>
            {
                try
                {
                    BorderFixHelper.FixAllBorders(window);
                }
                catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
                {
                    System.Diagnostics.Debug.WriteLine(
                        BorderFixDiagnosticsPolicy.FormatFailureMessage(
                            phase,
                            target,
                            ex.GetType().Name,
                            ex.Message));
                }
            });
    }

    private bool TryShowDialogWithDiagnostics(Window dialog, string dialogName)
    {
        var result = false;
        using var _ = FloatingTopmostDialogSuppressionState.Enter();
        SafeActionExecutionExecutor.TryExecute(
            () => DialogShowResultStateUpdater.MarkFromDialogResult(ref result, dialog.SafeShowDialog()),
            ex => System.Diagnostics.Debug.WriteLine(
                DialogShowDiagnosticsPolicy.FormatFailureMessage(
                    dialogName,
                    ex.Message)));
        return result;
    }

    private void ShowMainInfoMessageSafe(string operation, string message)
    {
        ExecuteLifecycleSafe(
            "main-message",
            operation,
            () => System.Windows.MessageBox.Show(
                this,
                message,
                "提示",
                MessageBoxButton.OK,
                MessageBoxImage.Information));
    }

    private void SaveSettings()
    {
        try
        {
            if (_overlayWindow != null &&
                _overlayWindow.TryGetStylusAdaptiveState(
                    out var pressureProfile,
                    out var sampleRateTier,
                    out var predictionHorizonMs,
                    out var calibratedLow,
                    out var calibratedHigh))
            {
                _settings.StylusAdaptivePressureProfile = pressureProfile;
                _settings.StylusAdaptiveSampleRateTier = sampleRateTier;
                _settings.StylusAdaptivePredictionHorizonMs = predictionHorizonMs;
                _settings.StylusPressureCalibratedLow = calibratedLow;
                _settings.StylusPressureCalibratedHigh = calibratedHigh;
            }

            _settingsService.Save(_settings);
            SettingsSaveFailureNotificationStateUpdater.MarkSaveSucceeded(ref _settingsSaveFailedNotified);
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            var notificationPlan = SettingsSaveFailureNotificationPolicy.Resolve(_settingsSaveFailedNotified);
            SettingsSaveFailureNotificationStateUpdater.ApplyNotificationPlan(
                ref _settingsSaveFailedNotified,
                notificationPlan);
            if (!notificationPlan.ShouldNotify)
            {
                return;
            }

            var detail = $"设置保存失败：{ex.Message}\n请检查设置文件权限或磁盘状态。";
            ShowMainInfoMessageSafe("settings-save-failed", detail);
        }
    }

    private void ScheduleInkCleanup()
    {
        _ = SafeTaskRunner.Run(
            "MainWindow.ScheduleInkCleanup",
            _ => TriggerInkCleanup(),
            _backgroundTasksCancellation.Token,
            ex => System.Diagnostics.Debug.WriteLine(
                InkStartupCleanupLogPolicy.FormatFailureMessage(ex.Message)));
    }

    private void TriggerInkCleanup()
    {
        try
        {
            var candidates = InkCleanupCandidateDirectoryPolicy.Resolve(
                AppDomain.CurrentDomain.BaseDirectory,
                _settings.InkPhotoRootPath,
                _settings.PhotoRecentFolders,
                _settings.PhotoFavoriteFolders,
                Directory.Exists);

            var totalSidecars = 0;
            var totalComposites = 0;
            foreach (var directory in candidates)
            {
                totalSidecars += _inkPersistenceService.CleanupOrphanSidecarsInDirectory(directory);
                totalComposites += _inkExportService.CleanupOrphanCompositeOutputsInDirectory(directory);
            }

            var summary = new InkStartupCleanupSummary(
                TotalSidecars: totalSidecars,
                TotalComposites: totalComposites);
            if (InkStartupCleanupLogPolicy.ShouldLogDeletionSummary(summary))
            {
                System.Diagnostics.Debug.WriteLine(InkStartupCleanupLogPolicy.FormatDeletionSummary(summary));
            }
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            System.Diagnostics.Debug.WriteLine(InkStartupCleanupLogPolicy.FormatFailureMessage(ex.Message));
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        var closingPlan = MainWindowOnClosingPlanPolicy.Resolve(_allowClose);
        if (!closingPlan.ShouldCancelClose)
        {
            return;
        }

        e.Cancel = closingPlan.ShouldCancelClose;
        if (closingPlan.ShouldRequestExit)
        {
            RequestExit();
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (_backgroundTasksCancellationDisposed)
        {
            return;
        }

        _backgroundTasksCancellationDisposed = true;
        Closed -= OnClosed;
        Closing -= OnClosing;
        Loaded -= OnLoaded;
        IsVisibleChanged -= OnMainWindowVisibleChanged;
        _autoExitTimer.Stop();
        _presentationForegroundSuppressionTimer.Stop();
        _floatingTopmostWatchdogTimer.Stop();
        _autoExitTimer.Tick -= OnAutoExitTimerTick;
        _presentationForegroundSuppressionTimer.Tick -= OnPresentationForegroundSuppressionTimerTick;
        _floatingTopmostWatchdogTimer.Tick -= OnFloatingTopmostWatchdogTick;
        ReleasePresentationForegroundSuppression();
        ExecuteLifecycleSafe("main-window-closed", "cancel-background-tasks", () => _backgroundTasksCancellation.Cancel());
        _backgroundTasksCancellation.Dispose();
    }
}
