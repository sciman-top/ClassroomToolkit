using System;
using System.Collections.Generic;
using System.Windows;
using ClassroomToolkit.App.Settings;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App;

public partial class MainWindow
{
    // ── Roll-call ──

    private void OnRollCallClick(object sender, RoutedEventArgs e)
    {
        EnsureRollCallWindow();
        if (_rollCallWindow == null)
        {
            return;
        }

        var transitionPlan = RollCallVisibilityTransitionPolicy.Resolve(
            CaptureRollCallVisibilityTransitionContext());
        ApplyRollCallTransition(transitionPlan);
        UpdateToggleButtons();
    }

    private RollCallVisibilityTransitionContext CaptureRollCallVisibilityTransitionContext()
    {
        return new RollCallVisibilityTransitionContext(
            RollCallVisible: _rollCallWindow?.IsVisible == true,
            RollCallActive: _rollCallWindow?.IsActive == true,
            OverlayVisible: _overlayWindow?.IsVisible == true);
    }

    private void ApplyRollCallTransition(RollCallVisibilityTransitionPlan transitionPlan)
    {
        if (_rollCallWindow == null)
        {
            return;
        }

        if (transitionPlan.HideWindow)
        {
            ExecuteLifecycleSafe("rollcall-toggle", "hide-rollcall-window", _rollCallWindow.HideRollCall);
        }

        if (transitionPlan.SyncOwnerToOverlay)
        {
            SyncOverlayOwnedWindow(_rollCallWindow);
        }

        if (transitionPlan.ShowWindow)
        {
            ExecuteLifecycleSafe("rollcall-toggle", "show-rollcall-window", _rollCallWindow.Show);
        }

        UserInitiatedWindowExecutionExecutor.Apply(
            _rollCallWindow,
            transitionPlan.ActivateWindow);
        FloatingZOrderApplyExecutor.Apply(
            transitionPlan.RequestZOrderApply,
            transitionPlan.ForceEnforceZOrder,
            RequestApplyZOrderPolicy);
    }

    private void EnsureRollCallWindow()
    {
        if (_rollCallWindow != null)
        {
            return;
        }

        var path = ResolveStudentWorkbookPath();
        _rollCallWindow = _rollCallWindowFactory.Create(path);
        WireRollCallWindow(_rollCallWindow);
    }

    private void WireRollCallWindow(RollCallWindow rollCallWindow)
    {
        rollCallWindow.IsVisibleChanged += OnRollCallWindowVisibleChanged;
        rollCallWindow.Closed += OnRollCallWindowClosed;
    }

    internal void HideRollCallWindowFromChildRequest()
    {
        if (_rollCallWindow == null || !_rollCallWindow.IsVisible)
        {
            return;
        }

        var transitionPlan = RollCallVisibilityTransitionPolicy.Resolve(
            CaptureRollCallVisibilityTransitionContext());
        ApplyRollCallTransition(transitionPlan);
        UpdateToggleButtons();
    }

    private void OnRollCallWindowVisibleChanged(object? sender, DependencyPropertyChangedEventArgs e)
    {
        UpdateToggleButtons();
    }

    private void OnRollCallWindowClosed(object? sender, EventArgs e)
    {
        if (sender is RollCallWindow closedWindow)
        {
            closedWindow.IsVisibleChanged -= OnRollCallWindowVisibleChanged;
            closedWindow.Closed -= OnRollCallWindowClosed;
        }

        _rollCallWindow = null;
        UpdateToggleButtons();
    }

    private void UpdateToggleButtons()
    {
        var toggleState = ViewModels.MainWindowToggleStatePolicy.Resolve(
            overlayVisible: _overlayWindow?.IsVisible == true,
            rollCallVisible: _rollCallWindow?.IsVisible == true);
        _mainViewModel.IsPaintActive = toggleState.IsPaintActive;
        _mainViewModel.IsRollCallVisible = toggleState.IsRollCallVisible;
    }

    private void OnOpenRollCallSettings()
    {
        var dialog = new RollCallSettingsDialog(_settings, ResolveAvailableClasses())
        {
            Owner = this
        };
        if (!TryShowDialogWithDiagnostics(dialog, nameof(RollCallSettingsDialog)))
        {
            return;
        }

        var patch = new RollCallSettingsPatch(
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
        RollCallSettingsApplier.Apply(_settings, patch);
        SaveSettings();
        _rollCallWindow?.ApplySettings(_settings);
    }

    private IReadOnlyList<string> ResolveAvailableClasses()
    {
        return _rollCallWindow?.AvailableClasses ?? Array.Empty<string>();
    }
}
