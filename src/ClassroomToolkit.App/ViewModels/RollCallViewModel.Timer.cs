using ClassroomToolkit.Domain.Timers;

namespace ClassroomToolkit.App.ViewModels;

public sealed partial class RollCallViewModel
{
    public void ToggleTimerMode()
    {
        if (_timerEngine.Running) return;

        var nextMode = _timerEngine.Mode switch
        {
            TimerMode.Countdown => TimerMode.Stopwatch,
            TimerMode.Stopwatch => TimerMode.Clock,
            _ => TimerMode.Countdown
        };

        _timerEngine.SetMode(nextMode);
        ApplyReminderInterval();
        UpdateTimeDisplay();
        RaisePropertyChanged(nameof(TimerModeLabel), nameof(StartPauseLabel), nameof(CurrentTimerMode));
    }

    public void ToggleTimer()
    {
        if (_timerEngine.Mode == TimerMode.Clock) return;

        if (!_timerEngine.Running && _timerEngine.Mode == TimerMode.Countdown && _timerEngine.SecondsLeft <= 0)
        {
            _timerEngine.Reset();
            UpdateTimeDisplay();
        }

        if (_timerEngine.Running) _timerEngine.Pause();
        else _timerEngine.Start();

        RaisePropertyChanged(nameof(StartPauseLabel), nameof(TimerRunning));
    }

    public void ResetTimer()
    {
        if (_timerEngine.Running) _timerEngine.Pause();
        _timerEngine.Reset();
        UpdateTimeDisplay();
        RaisePropertyChanged(nameof(StartPauseLabel), nameof(TimerRunning));
    }

    public void SetCountdown(int minutes, int seconds)
    {
        _timerMinutes = Math.Clamp(minutes, 0, 150);
        _timerSeconds = Math.Clamp(seconds, 0, 59);
        _timerEngine.SetCountdown(_timerMinutes, _timerSeconds);
        UpdateTimeDisplay();
        RaisePropertyChanged(nameof(StartPauseLabel), nameof(TimerRunning));
    }

    internal void ApplyTimerState(bool isRollCallMode, TimerMode timerMode, int minutes, int seconds, int secondsLeft, int stopwatchSeconds, bool running)
    {
        IsRollCallMode = isRollCallMode;
        _timerMinutes = minutes;
        _timerSeconds = seconds;
        _timerEngine.SetState(timerMode, minutes * 60 + seconds, secondsLeft, stopwatchSeconds, running);
        ApplyReminderInterval();
        UpdateTimeDisplay();
        RaisePropertyChanged(nameof(TimerModeLabel), nameof(StartPauseLabel), nameof(TimerRunning), nameof(TimerSecondsLeft), nameof(CurrentTimerMode), nameof(IsRollCallMode));
    }

    internal void TickTimer(TimeSpan elapsed)
    {
        _timerEngine.Tick(elapsed);
        UpdateTimeDisplay();
        if (_timerEngine.Mode == TimerMode.Countdown)
        {
            RaisePropertyChanged(nameof(TimerSecondsLeft));
        }
        else if (_timerEngine.Mode == TimerMode.Stopwatch)
        {
            RaisePropertyChanged(nameof(TimerStopwatchSeconds));
        }
    }

    private void UpdateTimeDisplay()
    {
        TimeDisplay = _timerEngine.Mode switch
        {
            TimerMode.Stopwatch => FormatTime(_timerEngine.StopwatchSeconds),
            TimerMode.Clock => DateTime.Now.ToString("HH:mm:ss"),
            _ => FormatTime(_timerEngine.SecondsLeft)
        };
    }

    private string FormatTime(int totalSeconds)
    {
        int h = totalSeconds / 3600;
        int m = (totalSeconds % 3600) / 60;
        int s = totalSeconds % 60;
        return h > 0 ? $"{h:D2}:{m:D2}:{s:D2}" : $"{m:D2}:{s:D2}";
    }

    private void ApplyReminderInterval()
    {
        if (TimerReminderEnabled && _timerEngine.Mode != TimerMode.Clock)
        {
            _timerEngine.ReminderIntervalSeconds = TimerReminderIntervalMinutes * 60;
        }
        else
        {
            _timerEngine.ReminderIntervalSeconds = 0;
        }
    }
}
