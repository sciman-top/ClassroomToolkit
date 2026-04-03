using System.Diagnostics;

namespace ClassroomToolkit.Domain.Timers;

public sealed class TimerEngine
{
    private int _countdownSeconds;
    private int _secondsLeft;
    private int _stopwatchSeconds;
    private int _reminderSeconds;
    private int _reminderCounter;
    private TimeSpan _pendingElapsed = TimeSpan.Zero;

    public TimerMode Mode { get; private set; } = TimerMode.Countdown;

    public bool Running { get; private set; }

    public int SecondsLeft => _secondsLeft;

    public int StopwatchSeconds => _stopwatchSeconds;

    public int CountdownSeconds => _countdownSeconds;

    public int ReminderIntervalSeconds
    {
        get => _reminderSeconds;
        set => _reminderSeconds = Math.Max(0, value);
    }

    public event Action? TimerCompleted;

    public event Action? ReminderTriggered;

    public void SetMode(TimerMode mode)
    {
        if (Mode == mode)
        {
            return;
        }
        Mode = mode;
        Running = false;
        _reminderCounter = 0;
        _pendingElapsed = TimeSpan.Zero;
        if (Mode == TimerMode.Countdown)
        {
            _secondsLeft = _countdownSeconds;
        }
        else if (Mode == TimerMode.Stopwatch)
        {
            _stopwatchSeconds = 0;
        }
    }

    public void SetCountdown(int minutes, int seconds)
    {
        var total = (long)minutes * 60L + seconds;
        if (total <= 0)
        {
            _countdownSeconds = 0;
        }
        else if (total >= int.MaxValue)
        {
            _countdownSeconds = int.MaxValue;
        }
        else
        {
            _countdownSeconds = (int)total;
        }
        _secondsLeft = _countdownSeconds;
        _reminderCounter = 0;
        _pendingElapsed = TimeSpan.Zero;
        Running = false;
    }

    public void SetState(TimerMode mode, int countdownSeconds, int secondsLeft, int stopwatchSeconds, bool running)
    {
        Mode = mode;
        _countdownSeconds = Math.Max(0, countdownSeconds);
        _secondsLeft = Math.Max(0, Math.Min(secondsLeft, _countdownSeconds));
        _stopwatchSeconds = Math.Max(0, stopwatchSeconds);
        _reminderCounter = 0;
        _pendingElapsed = TimeSpan.Zero;
        Running = mode != TimerMode.Clock && running;
    }

    public void Start()
    {
        if (Mode == TimerMode.Clock)
        {
            Running = false;
            return;
        }
        Running = true;
    }

    public void Pause()
    {
        Running = false;
    }

    public void Toggle()
    {
        if (Mode == TimerMode.Clock)
        {
            Running = false;
            return;
        }
        Running = !Running;
    }

    public void Reset()
    {
        Running = false;
        _reminderCounter = 0;
        _pendingElapsed = TimeSpan.Zero;
        if (Mode == TimerMode.Countdown)
        {
            _secondsLeft = _countdownSeconds;
        }
        else if (Mode == TimerMode.Stopwatch)
        {
            _stopwatchSeconds = 0;
        }
    }

    public void Tick(TimeSpan elapsed)
    {
        if (!Running || Mode == TimerMode.Clock)
        {
            return;
        }
        if (elapsed <= TimeSpan.Zero)
        {
            return;
        }

        _pendingElapsed += elapsed;
        var totalSeconds = (int)Math.Floor(Math.Min(_pendingElapsed.TotalSeconds, int.MaxValue));
        if (totalSeconds <= 0)
        {
            return;
        }
        _pendingElapsed -= TimeSpan.FromSeconds(totalSeconds);

        if (Mode == TimerMode.Stopwatch)
        {
            var next = (long)_stopwatchSeconds + totalSeconds;
            _stopwatchSeconds = next >= int.MaxValue ? int.MaxValue : (int)Math.Max(0, next);
            return;
        }
        if (Mode == TimerMode.Countdown)
        {
            var tickSeconds = Math.Min(totalSeconds, _secondsLeft);
            if (tickSeconds <= 0)
            {
                return;
            }
            _secondsLeft -= tickSeconds;
            TriggerReminder(tickSeconds);
            if (_secondsLeft == 0)
            {
                Running = false;
                InvokeEventSafely(TimerCompleted, "TimerCompleted");
            }
        }
    }

    private void TriggerReminder(int elapsedSeconds)
    {
        if (_reminderSeconds <= 0 || Mode != TimerMode.Countdown || elapsedSeconds <= 0)
        {
            return;
        }
        var total = _reminderCounter + elapsedSeconds;
        var triggers = total / _reminderSeconds;
        _reminderCounter = total % _reminderSeconds;
        if (triggers <= 0)
        {
            return;
        }

        // Do not emit a "midway reminder" exactly at the completion boundary.
        // When countdown length is an exact multiple of reminder interval,
        // completion already provides the terminal feedback signal.
        if (_secondsLeft == 0
            && _countdownSeconds > 0
            && _countdownSeconds % _reminderSeconds == 0
            && triggers > 0)
        {
            triggers--;
        }

        if (triggers <= 0)
        {
            return;
        }

        // Cap triggers to avoid sound storm on large elapsed jumps
        triggers = Math.Min(triggers, 3);
        for (var i = 0; i < triggers; i++)
        {
            InvokeEventSafely(ReminderTriggered, "ReminderTriggered");
        }
    }

    private static void InvokeEventSafely(Action? callback, string callbackName)
    {
        if (callback is null)
        {
            return;
        }

        foreach (var handler in callback.GetInvocationList())
        {
            try
            {
                ((Action)handler).Invoke();
            }
            catch (Exception ex) when (DomainExceptionFilterPolicy.IsNonFatal(ex))
            {
                Debug.WriteLine($"[TimerEngine] {callbackName} callback failed: {ex.GetType().Name} - {ex.Message}");
            }
        }
    }
}
