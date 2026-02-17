using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ClassroomToolkit.App.ViewModels;
using ClassroomToolkit.App.Utilities;
using ClassroomToolkit.App.Models;
using ClassroomToolkit.Domain.Utilities;
using ClassroomToolkit.Interop.Presentation;
using ClassroomToolkit.App.Settings;

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
            UpdatePhotoDisplay();
            SpeakStudentName();
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
            ? "确定要重置所有分组的点名状态并重新开始吗？"
            : $"确定要重置“{group}”分组的点名状态并重新开始吗？";
        var result = System.Windows.MessageBox.Show(prompt, "提示", MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (result != MessageBoxResult.OK)
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
        if (dialog.ShowDialog() == true && dialog.SelectedIndex.HasValue)
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
        if (dialog.ShowDialog() != true)
        {
            return;
        }
        var patch = BuildPatchFromDialog(dialog);
        RollCallSettingsApplier.Apply(_settings, patch);
        SaveSettingsSafe();
        ApplySettings(_settings, updatePhoto: false);
        HidePhotoOverlay();
        _lastPhotoStudentId = null;
    }

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Escape)
        {
            return;
        }
        if (_photoOverlay != null && _photoOverlay.IsVisible)
        {
            _photoOverlay.CloseOverlay();
            e.Handled = true;
        }
    }

    private async Task StartKeyboardHookCoreAsync(Func<bool> isCurrent)
    {
        if (!ShouldEnableRemotePresenterHook()) return;

        var fallback = new ClassroomToolkit.Interop.Presentation.KeyBinding(VirtualKey.Tab, KeyModifiers.None);
        var bindings = ResolveRemoteBindings(_viewModel.RemotePresenterKey, fallback);
        
        Action<ClassroomToolkit.Interop.Presentation.KeyBinding> handler = _ =>
        {
             Dispatcher.Invoke(() =>
             {
                 if (!_viewModel.IsRollCallMode) return;
                 
                 if (_viewModel.TryRollNext(out var message))
                 {
                     UpdatePhotoDisplay();
                     SpeakStudentName();
                     ScheduleRollStateSave();
                     return;
                 }
                 if (!string.IsNullOrWhiteSpace(message))
                 {
                     ShowRollCallMessage(message);
                 }
             });
        };

        bool result = await _hookService.RegisterHookAsync(bindings, handler, () => isCurrent() && ShouldEnableRemotePresenterHook());
        
        if (!result && !_remoteHookUnavailableNotified && isCurrent() && ShouldEnableRemotePresenterHook())
        {
             NotifyRemoteHookError();
        }
    }

    private void StopKeyboardHook()
    {
        _hookService.UnregisterAll();
    }

    private async Task StartGroupSwitchHookCoreAsync(Func<bool> isCurrent)
    {
        if (!ShouldEnableGroupSwitchHook()) return;

        var fallback = new ClassroomToolkit.Interop.Presentation.KeyBinding(VirtualKey.B, KeyModifiers.None);
        var bindings = ResolveRemoteBindings(_viewModel.RemoteGroupSwitchKey, fallback);
        
        Action<ClassroomToolkit.Interop.Presentation.KeyBinding> handler = _ =>
        {
            Dispatcher.Invoke(() =>
            {
                if (!_viewModel.IsRollCallMode) return;
                
                _viewModel.SwitchToNextGroup();
                ShowGroupOverlay();
                ScheduleRollStateSave();
            });
        };

        await _hookService.RegisterHookAsync(bindings, handler, () => isCurrent() && ShouldEnableGroupSwitchHook());
    }

    private void NotifyRemoteHookError()
    {
        if (_remoteHookUnavailableNotified) return;
        _remoteHookUnavailableNotified = true;
        Dispatcher.BeginInvoke(() =>
        {
            var owner = System.Windows.Application.Current?.MainWindow;
            var message = $"翻页笔全局监听不可用，可能被系统权限或安全软件拦截。可尝试以管理员身份运行。";
            System.Windows.MessageBox.Show(owner ?? this, message, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        });
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

    private static bool IsUnsupportedRemoteBinding(ClassroomToolkit.Interop.Presentation.KeyBinding binding)
    {
        return binding.Modifiers == KeyModifiers.None
            && binding.Key == VirtualKey.W; // W (removed)
    }

    private static IReadOnlyList<ClassroomToolkit.Interop.Presentation.KeyBinding> ResolveRemoteBindings(
        string configuredKey,
        ClassroomToolkit.Interop.Presentation.KeyBinding fallback)
    {
        if (string.Equals(configuredKey?.Trim(), "f5", StringComparison.OrdinalIgnoreCase))
        {
            return new[]
            {
                new ClassroomToolkit.Interop.Presentation.KeyBinding(VirtualKey.F5, KeyModifiers.None),
                new ClassroomToolkit.Interop.Presentation.KeyBinding(VirtualKey.F5, KeyModifiers.Shift),
                new ClassroomToolkit.Interop.Presentation.KeyBinding(VirtualKey.Escape, KeyModifiers.None)
            };
        }

        var binding = KeyBindingParser.ParseOrDefault(configuredKey, fallback);
        if (IsUnsupportedRemoteBinding(binding))
        {
            binding = fallback;
        }
        return new[] { binding };
    }

    private void OpenRemoteKeyDialog()
    {
        var dialog = new RemoteKeyDialog(_viewModel.RemotePresenterKey)
        {
            Owner = this
        };
        if (dialog.ShowDialog() == true)
        {
            _viewModel.SetRemotePresenterKey(dialog.SelectedKey);
            _settings.RemotePresenterKey = _viewModel.RemotePresenterKey;
            SaveSettingsSafe();
            RestartKeyboardHook();
        }
    }
}
