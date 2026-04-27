using System;
using System.Media;
using System.Windows;
using ClassroomToolkit.App.Settings;

namespace ClassroomToolkit.App;

public partial class RollCallWindow
{
    private void OnTimerModeClick(object sender, RoutedEventArgs e)
    {
        _viewModel.ToggleTimerMode();
    }

    private void OnTimerStartPauseClick(object sender, RoutedEventArgs e)
    {
        var wasRunning = _viewModel.TimerRunning;
        _viewModel.ToggleTimer();
        if (!wasRunning && _viewModel.TimerRunning)
        {
            // Avoid consuming paused-time delta on the first running tick.
            _stopwatch.Restart();
        }
    }

    private void OnTimerResetClick(object sender, RoutedEventArgs e)
    {
        _viewModel.ResetTimer();
        _stopwatch.Restart();
    }

    private void OnTimerSetClick(object sender, RoutedEventArgs e)
    {
        var dialog = new TimerSetDialog(_viewModel.TimerMinutes, _viewModel.TimerSeconds)
        {
            Owner = this
        };
        if (TryShowDialogSafe(dialog, nameof(TimerSetDialog)))
        {
            _viewModel.SetCountdown(dialog.Minutes, dialog.Seconds);
            _stopwatch.Restart();
        }
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        var elapsed = _stopwatch.Elapsed;
        _stopwatch.Restart();
        _viewModel.TickTimer(elapsed);
    }

    private void OnTimerCompleted()
    {
        if (_viewModel.TimerSoundEnabled)
        {
            PlayTimerSound(_viewModel.TimerSoundVariant, isReminder: false);
        }
    }

    private void OnReminderTriggered()
    {
        if (_viewModel.TimerReminderEnabled)
        {
            PlayTimerSound(_viewModel.TimerReminderSoundVariant, isReminder: true);
        }
    }

    private static void PlayTimerSound(string? variant, bool isReminder)
    {
        var key = (variant ?? string.Empty).Trim().ToUpperInvariant();
        switch (key)
        {
            case "BELL":
                SystemSounds.Exclamation.Play();
                break;
            case "DIGITAL":
                SystemSounds.Beep.Play();
                break;
            case "BUZZ":
                SystemSounds.Hand.Play();
                break;
            case "URGENT":
                SystemSounds.Hand.Play();
                break;
            case "PING":
                SystemSounds.Beep.Play();
                break;
            case "CHIME":
                SystemSounds.Asterisk.Play();
                break;
            case "PULSE":
                SystemSounds.Asterisk.Play();
                break;
            case "SHORT_BELL":
                SystemSounds.Exclamation.Play();
                break;
            default:
                if (isReminder)
                {
                    SystemSounds.Asterisk.Play();
                }
                else
                {
                    SystemSounds.Exclamation.Play();
                }
                break;
        }
    }
}
