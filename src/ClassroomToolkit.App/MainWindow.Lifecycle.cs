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
    private readonly ApplicationExitCoordinator _exitCoordinator = new();

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

        var launcherVisible = LauncherWindowPolicies.IsVisibleForTopmost(
            launcherMinimized: _settings.LauncherMinimized,
            mainVisible: IsVisible,
            mainMinimized: WindowState == WindowState.Minimized,
            bubbleVisible: _bubbleWindow?.IsVisible == true,
            bubbleMinimized: _bubbleWindow?.WindowState == WindowState.Minimized);
        var shouldRetouch = FloatingTopmostPolicies.ShouldForceRetouch(
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
        if (IsVisible)
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
        if (_settings.LauncherMinimized)
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
        if (_allowClose)
        {
            return;
        }

        var shouldCancelBackgroundTasks = !_backgroundTasksCancellation.IsCancellationRequested;
        var hasBubbleWindow = _bubbleWindow != null;
        var hasRollCallWindow = _rollCallWindow != null;
        var hasImageManagerWindow = _imageManagerWindow != null;
        _exitCoordinator.TryExit(
            saveSettings: () =>
            {
                CapturePaintToolbarPosition(save: false);
                return SaveLauncherSettings();
            },
            confirmDiscardSettings: ConfirmExitWithoutSavingSettings,
            prepareChildWindows: discardSettings =>
                _rollCallWindow?.TryPrepareForApplicationExit(discardSettings) ?? true,
            shutdown: () => CompleteExit(
                shouldCancelBackgroundTasks,
                hasBubbleWindow,
                hasRollCallWindow,
                hasImageManagerWindow));
    }

    private bool ConfirmExitWithoutSavingSettings()
    {
        var reason = _settingsService.IsOverwriteBlocked
            ? "设置文件无法安全读取，当前处于保护模式，原文件将保留。"
            : "本次设置未能保存。";
        return TopmostMessageBox.Show(
            this,
            $"{reason}\n\n是否放弃本次设置更改并退出？\n点名记录仍须成功保存才能退出。",
            "退出课堂工具箱",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No) == MessageBoxResult.Yes;
    }

    private void CompleteExit(
        bool shouldCancelBackgroundTasks,
        bool shouldCloseBubbleWindow,
        bool shouldCloseRollCallWindow,
        bool shouldCloseImageManagerWindow)
    {
        const string phase = "request-exit";

        _allowClose = true;

        if (shouldCancelBackgroundTasks)
        {
            ExecuteLifecycleSafe(phase, "cancel-background-tasks", () => _backgroundTasksCancellation.Cancel());
        }

        ExecuteLifecycleSafe(phase, "reset-toolbar-retouch-runtime", () => ResetToolbarInteractionRetouchRuntime(ToolbarInteractionRetouchRuntimeResetReason.RequestExit));
        // 孤儿 sidecar 清理要枚举多个目录，放后台执行；退出不等它，未完成的清理由下次启动兜底。
        _ = Task.Run(TriggerInkCleanup);
        if (shouldCloseBubbleWindow && _bubbleWindow != null)
        {
            var bubbleWindow = _bubbleWindow;
            ExecuteLifecycleSafe(phase, "close-launcher-bubble-window", bubbleWindow.Close);
            _bubbleWindow = null;
        }

        if (shouldCloseRollCallWindow && _rollCallWindow != null)
        {
            var rollCallWindow = _rollCallWindow;
            ExecuteLifecycleSafe(phase, "close-rollcall-window", rollCallWindow.RequestClose);
            _rollCallWindow = null;
        }

        if (shouldCloseImageManagerWindow && _imageManagerWindow != null)
        {
            ExecuteLifecycleSafe(phase, "close-image-manager-window", _imageManagerWindowCoordinator.Close);
        }

        ExecuteLifecycleSafe(phase, "close-paint-window-orchestrator", _paintWindowOrchestrator.Close);
        ExecuteLifecycleSafe(phase, "shutdown-application", () => System.Windows.Application.Current.Shutdown());
    }

    private bool ShouldIgnoreShutdownTicks()
    {
        return _backgroundTasksCancellationDisposed || _backgroundTasksCancellation.IsCancellationRequested;
    }

    private static void ExecuteLifecycleSafe(string phase, string operation, Action action)
    {
        SafeActionExecutionExecutor.TryExecute(
            action,
            ex => System.Diagnostics.Debug.WriteLine(
                WindowingDiagnosticsPolicies.FormatFailureMessageLifecycleSafeExecutionDiagnostics(
                    phase,
                    operation,
                    ex.GetType().Name,
                    ex.Message)));
    }

    private static void TryFixWindowBorders(Window window, string phase, string target)
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
                catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandling.IsNonFatal(ex))
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[BorderFix] failed phase={phase} target={target} ex={ex.GetType().Name} msg={ex.Message}");
                }
            });
    }

    private static bool TryShowDialogWithDiagnostics(Window dialog, string dialogName)
    {
        var result = false;
        using var _ = FloatingTopmostDialogSuppressionState.Enter();
        SafeActionExecutionExecutor.TryExecute(
            () => result = dialog.SafeShowDialog() == true,
            ex => System.Diagnostics.Debug.WriteLine(
                WindowingDiagnosticsPolicies.FormatFailureMessageDialogShowDiagnostics(
                    dialogName,
                    ex.Message)));
        return result;
    }

    private void ShowMainInfoMessageSafe(string operation, string message)
    {
        ExecuteLifecycleSafe(
            "main-message",
            operation,
            () => TopmostMessageBox.Show(
                this,
                message,
                "提示",
                MessageBoxButton.OK,
                MessageBoxImage.Information));
    }

    private bool SaveSettings()
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
            _settingsSaveFailedNotified = false;
            return true;
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandling.IsNonFatal(ex))
        {
            var shouldNotify = !_settingsSaveFailedNotified;
            _settingsSaveFailedNotified = true;
            if (!shouldNotify)
            {
                return false;
            }

            var detail = $"设置保存失败：{ex.Message}\n请检查设置文件权限或磁盘状态。";
            ShowMainInfoMessageSafe("settings-save-failed", detail);
            return false;
        }
    }

    private void ScheduleInkCleanup()
    {
        _ = SafeTaskRunner.Run(
            "MainWindow.ScheduleInkCleanup",
            _ => TriggerInkCleanup(),
            _backgroundTasksCancellation.Token,
            ex => System.Diagnostics.Debug.WriteLine(
                NotificationAndExitPolicies.FormatFailureMessage(ex.Message)));
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
            if (NotificationAndExitPolicies.ShouldLogDeletionSummary(summary))
            {
                System.Diagnostics.Debug.WriteLine(NotificationAndExitPolicies.FormatDeletionSummary(summary));
            }
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandling.IsNonFatal(ex))
        {
            System.Diagnostics.Debug.WriteLine(NotificationAndExitPolicies.FormatFailureMessage(ex.Message));
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        e.Cancel = true;
        RequestExit();
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
