using System;
using System.Threading.Tasks;
using System.Windows;
using ClassroomToolkit.App.ViewModels;
using ClassroomToolkit.Domain.Timers;
using ClassroomToolkit.App.Settings;
using ClassroomToolkit.App.RollCall;
using ClassroomToolkit.App.Utilities;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App;

public partial class RollCallWindow
{
    private void OnDataLoadFailed(string message)
    {
        var owner = System.Windows.Application.Current?.MainWindow;
        var detail = string.IsNullOrWhiteSpace(message) ? "学生名册读取失败，请检查文件是否被占用或已损坏。" : message;
        ShowRollCallInfoMessageSafe("data-load-failed", detail, owner);
    }

    private void OnDataSaveFailed(string message)
    {
        var owner = System.Windows.Application.Current?.MainWindow;
        var detail = string.IsNullOrWhiteSpace(message) ? "学生名册保存失败，请关闭 Excel 后重试。" : message;
        ShowRollCallInfoMessageSafe("data-save-failed", detail, owner);
    }

    private void ScheduleRollStateSave()
    {
        _rollStateDirty = true;
        if (_rollStateSaveTimer.IsEnabled)
        {
            _rollStateSaveTimer.Stop();
        }
        _rollStateSaveTimer.Start();
    }

    private void OnRollStateSaveTick(object? sender, EventArgs e)
    {
        _rollStateSaveTimer.Stop();
        if (!_rollStateDirty)
        {
            return;
        }
        _rollStateDirty = false;
        _viewModel.SaveState();
    }

    public void ApplySettings(AppSettings settings, bool updatePhoto = true)
    {
        _viewModel.ShowId = settings.RollCallShowId;
        _viewModel.ShowName = settings.RollCallShowName;
        if (!_viewModel.ShowId && !_viewModel.ShowName)
        {
            _viewModel.ShowName = true;
        }
        _viewModel.ShowPhoto = settings.RollCallShowPhoto;
        _viewModel.PhotoDurationSeconds = settings.RollCallPhotoDurationSeconds;
        _viewModel.PhotoSharedClass = settings.RollCallPhotoSharedClass;
        _viewModel.TimerSoundEnabled = settings.RollCallTimerSoundEnabled;
        _viewModel.TimerReminderEnabled = settings.RollCallTimerReminderEnabled;
        _viewModel.TimerReminderIntervalMinutes = settings.RollCallTimerReminderIntervalMinutes;
        _viewModel.TimerSoundVariant = settings.RollCallTimerSoundVariant;
        _viewModel.TimerReminderSoundVariant = settings.RollCallTimerReminderSoundVariant;
        _viewModel.SpeechEnabled = settings.RollCallSpeechEnabled;
        _viewModel.SpeechEngine = settings.RollCallSpeechEngine;
        _viewModel.SpeechVoiceId = settings.RollCallSpeechVoiceId;
        _viewModel.SpeechOutputId = settings.RollCallSpeechOutputId;
        _viewModel.RemotePresenterEnabled = settings.RollCallRemoteEnabled;
        _viewModel.RemoteGroupSwitchEnabled = settings.RollCallRemoteGroupSwitchEnabled;
        _viewModel.SetRemotePresenterKey(settings.RemotePresenterKey);
        _viewModel.RemoteGroupSwitchKey = settings.RemoteGroupSwitchKey;
        if (!_timerStateApplied)
        {
            var isRollCallMode = !string.Equals(settings.RollCallMode, "timer", StringComparison.OrdinalIgnoreCase);
            var timerMode = settings.RollCallTimerMode?.Trim().ToLowerInvariant() switch
            {
                "stopwatch" => TimerMode.Stopwatch,
                "clock" => TimerMode.Clock,
                _ => TimerMode.Countdown
            };
            _viewModel.ApplyTimerState(
                isRollCallMode,
                timerMode,
                settings.RollCallTimerMinutes,
                settings.RollCallTimerSeconds,
                settings.RollCallTimerSecondsLeft,
                settings.RollCallStopwatchSeconds,
                running: false);
            _timerStateApplied = true;
        }
        
        UpdateRemoteHookState();
        if (updatePhoto)
        {
            UpdatePhotoDisplay();
        }
    }

    private void PersistSettings()
    {
        CaptureWindowBounds();
        RollCallSettingsApplier.Apply(_settings, BuildPatchFromViewModel());
        _settings.RollCallMode = _viewModel.IsRollCallMode ? "roll_call" : "timer";
        _settings.RollCallTimerMode = _viewModel.CurrentTimerMode switch
        {
            TimerMode.Stopwatch => "stopwatch",
            TimerMode.Clock => "clock",
            _ => "countdown"
        };
        _settings.RollCallTimerMinutes = _viewModel.TimerMinutes;
        _settings.RollCallTimerSeconds = _viewModel.TimerSeconds;
        _settings.RollCallTimerSecondsLeft = _viewModel.TimerSecondsLeft;
        _settings.RollCallStopwatchSeconds = _viewModel.TimerStopwatchSeconds;
        _settings.RollCallTimerRunning = _viewModel.TimerRunning;
        _settings.RollCallCurrentClass = _viewModel.ActiveClassName;
        _settings.RollCallCurrentGroup = _viewModel.CurrentGroup;
        SaveSettingsSafe();
    }

    private void SaveSettingsSafe()
    {
        Exception? saveFailure = null;
        var saved = SafeActionExecutionExecutor.TryExecute(
            () =>
            {
                _settingsService.Save(_settings);
                return true;
            },
            fallback: false,
            onFailure: ex => saveFailure = ex);
        if (saved)
        {
            SettingsSaveFailureNotificationStateUpdater.MarkSaveSucceeded(ref _settingsSaveFailedNotified);
            return;
        }

        if (saveFailure != null)
        {
            var notificationPlan = SettingsSaveFailureNotificationPolicy.Resolve(_settingsSaveFailedNotified);
            SettingsSaveFailureNotificationStateUpdater.ApplyNotificationPlan(
                ref _settingsSaveFailedNotified,
                notificationPlan);
            if (!notificationPlan.ShouldNotify)
            {
                return;
            }
            var owner = System.Windows.Application.Current?.MainWindow;
            var detail = $"设置保存失败：{saveFailure.Message}\n请检查设置文件权限或磁盘状态。";
            ShowRollCallInfoMessageSafe("settings-save-failed", detail, owner);
        }
    }

    private static RollCallSettingsPatch BuildPatchFromDialog(RollCallSettingsDialog dialog)
    {
        return new RollCallSettingsPatch(
            dialog.RollCallShowId,
            dialog.RollCallShowName,
            dialog.RollCallShowPhoto,
            dialog.RollCallPhotoDurationSeconds,
            dialog.RollCallPhotoSharedClass,
            dialog.RollCallTimerSoundEnabled,
            dialog.RollCallTimerReminderEnabled,
            dialog.RollCallTimerReminderIntervalMinutes,
            dialog.RollCallTimerSoundVariant,
            dialog.RollCallTimerReminderSoundVariant,
            dialog.RollCallSpeechEnabled,
            dialog.RollCallSpeechEngine,
            dialog.RollCallSpeechVoiceId,
            dialog.RollCallSpeechOutputId,
            dialog.RollCallRemoteEnabled,
            dialog.RollCallRemoteGroupSwitchEnabled,
            dialog.RemotePresenterKey,
            dialog.RemoteGroupSwitchKey);
    }

    private RollCallSettingsPatch BuildPatchFromViewModel()
    {
        return new RollCallSettingsPatch(
            _viewModel.ShowId,
            _viewModel.ShowName,
            _viewModel.ShowPhoto,
            _viewModel.PhotoDurationSeconds,
            _viewModel.PhotoSharedClass,
            _viewModel.TimerSoundEnabled,
            _viewModel.TimerReminderEnabled,
            _viewModel.TimerReminderIntervalMinutes,
            _viewModel.TimerSoundVariant,
            _viewModel.TimerReminderSoundVariant,
            _viewModel.SpeechEnabled,
            _viewModel.SpeechEngine,
            _viewModel.SpeechVoiceId,
            _viewModel.SpeechOutputId,
            _viewModel.RemotePresenterEnabled,
            _viewModel.RemoteGroupSwitchEnabled,
            _viewModel.RemotePresenterKey,
            _viewModel.RemoteGroupSwitchKey);
    }

    private void RestoreGroupSelection()
    {
        var group = _settings.RollCallCurrentGroup;
        if (string.IsNullOrWhiteSpace(group))
        {
            return;
        }
        if (_viewModel.Groups.Contains(group))
        {
            _viewModel.SetCurrentGroup(group);
            UpdatePhotoDisplay(forceHide: true);
        }
    }

    private void TryApplyClassSelection(string selected)
    {
        if (_viewModel.SwitchClass(selected, updatePhoto: false))
        {
            UpdatePhotoDisplay(forceHide: true);
            PersistSettings();
            _viewModel.SaveState();
            UpdateMinWindowSize();
            SuppressRollClicks(TimeSpan.FromMilliseconds(RollCallRuntimeDefaults.ClassSwitchSuppressMs));
            return;
        }
        if (!string.Equals(_viewModel.ActiveClassName, selected, StringComparison.OrdinalIgnoreCase))
        {
            var classes = _viewModel.AvailableClasses?.Count > 0
                ? string.Join("、", _viewModel.AvailableClasses)
                : "（空）";
            var message = $"切换班级失败：{selected}\n当前班级：{_viewModel.ActiveClassName}\n可用班级：{classes}\n名册路径：{_dataPath}";
            ShowRollCallMessage(message);
        }
    }

    private void OpenClassSelectionDialog()
    {
        var classes = _viewModel.AvailableClasses;
        if (classes == null || classes.Count == 0)
        {
            ShowRollCallMessage("暂无班级可供选择。");
            return;
        }
        var dialog = new ClassSelectDialog(classes, _viewModel.ActiveClassName)
        {
            Owner = this
        };
        if (TryShowDialogSafe(dialog, nameof(ClassSelectDialog)) && !string.IsNullOrWhiteSpace(dialog.SelectedClass))
        {
            TryApplyClassSelection(dialog.SelectedClass);
        }
    }

    private void SuppressRollClicks(TimeSpan duration)
    {
        _suppressRollUntil = RollCallClickSuppressionPolicy.ExtendSuppressUntil(
            _suppressRollUntil,
            GetCurrentUtcTimestamp(),
            duration);
    }

    private bool ShouldSuppressRollClick()
    {
        return RollCallClickSuppressionPolicy.ShouldSuppress(
            _suppressRollUntil,
            GetCurrentUtcTimestamp());
    }

    private static DateTime GetCurrentUtcTimestamp() => DateTime.UtcNow;

    private void ShowRollCallMessage(string message)
    {
        ShowRollCallInfoMessageSafe("rollcall-message", message);
    }

    private void OnSpeechUnavailable()
    {
        NotifySpeechError();
    }
    
    private void SpeakStudentName()
    {
        var lifecycleToken = _lifecycleCancellation.Token;
        _ = SafeTaskRunner.Run(
            "RollCallWindow.SpeakStudentName",
            _ => SpeakStudentNameAsync(),
            lifecycleToken,
            onError: ex =>
            {
                System.Diagnostics.Debug.WriteLine($"SpeakStudentName failed: {ex.Message}");
                NotifySpeechError();
            });
    }

    private Task SpeakStudentNameAsync()
    {
        if (!_viewModel.SpeechEnabled)
        {
            return Task.CompletedTask;
        }

        var name = _viewModel.CurrentStudentName;
        if (string.IsNullOrWhiteSpace(name))
        {
            return Task.CompletedTask;
        }

        return _speechService.SpeakAsync(name, _viewModel.SpeechVoiceId);
    }

    private void NotifySpeechError()
    {
        if (!SpeechUnavailableNotificationPolicy.ShouldNotify(ref _speechUnavailableNotifiedState))
        {
            return;
        }
        if (!RollCallRemoteHookDispatchPolicy.CanDispatch(
                Dispatcher.HasShutdownStarted,
                Dispatcher.HasShutdownFinished))
        {
            System.Diagnostics.Debug.WriteLine("NotifySpeechError dispatch skipped: dispatcher-unavailable");
            return;
        }

        SafeActionExecutionExecutor.TryExecute(
            () =>
            {
                _ = Dispatcher.InvokeAsync(() =>
                {
                    var owner = System.Windows.Application.Current?.MainWindow;
                    var message = "语音播报不可用，可能缺少系统语音包或相关组件。请安装中文语音包后重启。";
                    ShowRollCallInfoMessageSafe("speech-unavailable", message, owner);
                });
            },
            ex => System.Diagnostics.Debug.WriteLine($"NotifySpeechError dispatch failed: {ex.Message}"));
    }
}

