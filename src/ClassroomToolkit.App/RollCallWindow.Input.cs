using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ClassroomToolkit.App.ViewModels;
using ClassroomToolkit.App.Utilities;
using ClassroomToolkit.App.Models;
using ClassroomToolkit.App.RollCall;
using ClassroomToolkit.Domain.Utilities;
using ClassroomToolkit.App.Settings;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App;

public partial class RollCallWindow
{
    private void OnGroupEntryClick(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button && button.DataContext is GroupButtonItem item)
        {
            if (item.IsReset)
            {
                OnResetClick(sender, e);
                return;
            }
            if (!string.IsNullOrWhiteSpace(item.Label))
            {
                _viewModel.SetCurrentGroup(item.Label);
                UpdatePhotoDisplay(forceHide: true);
                PersistSettings();
            }
        }
    }

    private void OnRollClick(object sender, RoutedEventArgs e)
    {
        if (ShouldSuppressRollClick())
        {
            return;
        }
        if (_viewModel.TryRollNext(out var message))
        {
            SpeakStudentName();
            UpdatePhotoDisplay();
            ScheduleRollStateSave();
            return;
        }
        if (!string.IsNullOrWhiteSpace(message))
        {
            ShowRollCallMessage(message);
        }
    }

    private void OnResetClick(object sender, RoutedEventArgs e)
    {
        var group = _viewModel.CurrentGroup;
        var prompt = group == ClassroomToolkit.Domain.Utilities.IdentityUtils.AllGroupName
            ? "确定要重置所有分组点名状态并重新开始吗？"
            : $"确定要重置“{group}”分组点名状态并重新开始吗？";
        if (!TryShowRollCallConfirmationSafe("reset-rollcall-group", prompt))
        {
            return;
        }
        _viewModel.ResetCurrentGroup();
        UpdatePhotoDisplay(forceHide: true);
        ScheduleRollStateSave();
    }

    private void OnToggleModeClick(object sender, RoutedEventArgs e)
    {
        _viewModel.ToggleMode();
        UpdateRemoteHookState();
        UpdatePhotoDisplay(forceHide: true);
        UpdateMinWindowSize();
    }

    private void OnClassSelectClick(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.IsRollCallMode)
        {
            return;
        }
        OpenClassSelectionDialog();
    }

    private void OnListClick(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.IsRollCallMode)
        {
            return;
        }
        if (!_viewModel.TryBuildStudentList(out var students, out var message))
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                ShowRollCallMessage(message);
            }
            return;
        }
        var dialog = new StudentListDialog(students)
        {
            Owner = this
        };
        if (TryShowDialogSafe(dialog, nameof(StudentListDialog)) && dialog.SelectedIndex.HasValue)
        {
            if (_viewModel.SetCurrentStudentByIndex(dialog.SelectedIndex.Value))
            {
                SpeakStudentName();
                UpdatePhotoDisplay();
            }
        }
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        var dialog = new RollCallSettingsDialog(_settings, _viewModel.AvailableClasses)
        {
            Owner = this
        };
        if (!TryShowDialogSafe(dialog, nameof(RollCallSettingsDialog)))
        {
            return;
        }
        var patch = BuildPatchFromDialog(dialog);
        RollCallSettingsApplier.Apply(_settings, patch);
        SaveSettingsSafe();
        ApplySettings(_settings, updatePhoto: false);
        HidePhotoOverlay();
    }

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Escape)
        {
            if (System.Windows.Application.Current?.MainWindow is MainWindow mainWindow
                && mainWindow.TryHandleOverlayNavigationKeyFromAuxWindow(e.Key))
            {
                e.Handled = true;
            }
            return;
        }
        if (_photoOverlay != null && _photoOverlay.IsVisible)
        {
            HidePhotoOverlay();
            e.Handled = true;
        }
    }

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (System.Windows.Application.Current?.MainWindow is not MainWindow mainWindow)
        {
            return;
        }

        if (mainWindow.TryHandleOverlayNavigationWheelFromAuxWindow(e.Delta))
        {
            e.Handled = true;
        }
    }

    private async Task StartKeyboardHookCoreAsync(Func<bool> isCurrent)
    {
        try
        {
            Action handler = () =>
            {
                EnqueueRemoteHookUiAction("roll", () =>
                {
                    RollCallRemoteHookActionExecutionCoordinator.ExecuteRoll(
                        _viewModel.IsRollCallMode,
                        _viewModel.TryRollNext,
                        () => UpdatePhotoDisplay(),
                        SpeakStudentName,
                        ScheduleRollStateSave,
                        ShowRollCallMessage);
                });
            };

            var request = new RollCallRemoteHookStartRequest(
                ShouldEnable: ShouldEnableRemotePresenterHook(),
                ConfiguredKey: _viewModel.RemotePresenterKey,
                FallbackToken: "tab",
                Handler: handler,
                ShouldKeepActive: () => isCurrent() && ShouldEnableRemotePresenterHook(),
                AlreadyUnavailableNotified: RemoteHookUnavailableNotificationPolicy.IsNotified(ref _remoteHookUnavailableNotifiedState),
                NotifyUnavailableOnFailure: true);
            var result = await _remoteHookCoordinator.TryStartAsync(request);
            if (result.Started)
            {
                RemoteHookUnavailableNotificationPolicy.Reset(ref _remoteHookUnavailableNotifiedState);
            }
            if (result.ShouldNotifyUnavailable)
            {
                 NotifyRemoteHookError();
            }
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            System.Diagnostics.Debug.WriteLine($"StartKeyboardHookCoreAsync failed: {ex}");
            if (isCurrent() && ShouldEnableRemotePresenterHook())
            {
                NotifyRemoteHookError();
            }
        }
    }

    private void StopKeyboardHook()
    {
        _remoteHookCoordinator.StopAllHooks();
    }

    private async Task StartGroupSwitchHookCoreAsync(Func<bool> isCurrent)
    {
        try
        {
            Action handler = () =>
            {
                EnqueueRemoteHookUiAction("group-switch", () =>
                {
                    RollCallRemoteHookActionExecutionCoordinator.ExecuteGroupSwitch(
                        _viewModel.IsRollCallMode,
                        _viewModel.SwitchToNextGroup,
                        ShowGroupOverlay,
                        ScheduleRollStateSave);
                });
            };
            var request = new RollCallRemoteHookStartRequest(
                ShouldEnable: ShouldEnableGroupSwitchHook(),
                ConfiguredKey: _viewModel.RemoteGroupSwitchKey,
                FallbackToken: "enter",
                Handler: handler,
                ShouldKeepActive: () => isCurrent() && ShouldEnableGroupSwitchHook(),
                AlreadyUnavailableNotified: false,
                NotifyUnavailableOnFailure: false);
            await _remoteHookCoordinator.TryStartAsync(request);
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            System.Diagnostics.Debug.WriteLine($"StartGroupSwitchHookCoreAsync failed: {ex}");
        }
    }

    private void EnqueueRemoteHookUiAction(string operation, Action action)
    {
        if (!RollCallRemoteHookDispatchPolicy.CanDispatch(
                Dispatcher.HasShutdownStarted,
                Dispatcher.HasShutdownFinished))
        {
            System.Diagnostics.Debug.WriteLine(
                RollCallWindowDiagnosticsPolicy.FormatRemoteHookDispatchSkippedMessage(
                    operation,
                    "dispatcher-unavailable"));
            return;
        }

        void ExecuteOnUi()
        {
            SafeActionExecutionExecutor.TryExecute(
                action,
                ex => System.Diagnostics.Debug.WriteLine(
                    RollCallWindowDiagnosticsPolicy.FormatRemoteHookDispatchFailureMessage(
                        operation,
                        ex.GetType().Name,
                        ex.Message)));
        }

        var scheduled = false;
        SafeActionExecutionExecutor.TryExecute(
            () =>
            {
                _ = Dispatcher.BeginInvoke(
                    DispatcherPriority.Normal,
                    new Action(ExecuteOnUi));
                scheduled = true;
            },
            ex => System.Diagnostics.Debug.WriteLine(
                RollCallWindowDiagnosticsPolicy.FormatRemoteHookDispatchFailureMessage(
                    operation,
                    ex.GetType().Name,
                    ex.Message)));
        if (!scheduled && Dispatcher.CheckAccess())
        {
            ExecuteOnUi();
        }
    }

    private void NotifyRemoteHookError()
    {
        if (!RemoteHookUnavailableNotificationPolicy.ShouldNotify(ref _remoteHookUnavailableNotifiedState))
        {
            return;
        }
        if (!RollCallRemoteHookDispatchPolicy.CanDispatch(
                Dispatcher.HasShutdownStarted,
                Dispatcher.HasShutdownFinished))
        {
            System.Diagnostics.Debug.WriteLine(
                RollCallWindowDiagnosticsPolicy.FormatRemoteHookDispatchSkippedMessage(
                    "remote-hook-unavailable",
                    "dispatcher-unavailable"));
            return;
        }

        void ShowUnavailableNotice()
        {
            var owner = System.Windows.Application.Current?.MainWindow;
            var message = "翻页笔监听不可用，可能被拦截。可尝试以管理员身份运行。";
            ShowRollCallInfoMessageSafe("remote-hook-unavailable", message, owner);
        }

        var scheduled = false;
        SafeActionExecutionExecutor.TryExecute(
            () =>
            {
                _ = Dispatcher.InvokeAsync(ShowUnavailableNotice);
                scheduled = true;
            },
            ex => System.Diagnostics.Debug.WriteLine($"NotifyRemoteHookError dispatch failed: {ex.Message}"));
        if (!scheduled && Dispatcher.CheckAccess())
        {
            ShowUnavailableNotice();
        }
    }

    private void UpdateRemoteHookState()
    {
        RestartKeyboardHook();
        UpdateGroupNameDisplay();
    }
    
    private void RestartKeyboardHook()
    {
        var generation = _remoteHookStartGate.NextGeneration();
        StopKeyboardHook();
        _ = _remoteHookStartGate.RunAsync(generation, StartKeyboardHookCoreAsync);
        _ = _remoteHookStartGate.RunAsync(generation, StartGroupSwitchHookCoreAsync);
    }

    private bool ShouldEnableRemotePresenterHook() => _viewModel.RemotePresenterEnabled && _viewModel.IsRollCallMode;

    private bool ShouldEnableGroupSwitchHook() => _viewModel.RemoteGroupSwitchEnabled && _viewModel.IsRollCallMode;

    private void OpenRemoteKeyDialog()
    {
        var dialog = new RemoteKeyDialog(_viewModel.RemotePresenterKey)
        {
            Owner = this
        };
        if (TryShowDialogSafe(dialog, nameof(RemoteKeyDialog)))
        {
            _viewModel.SetRemotePresenterKey(dialog.SelectedKey);
            _settings.RemotePresenterKey = _viewModel.RemotePresenterKey;
            SaveSettingsSafe();
            RestartKeyboardHook();
        }
    }
}
