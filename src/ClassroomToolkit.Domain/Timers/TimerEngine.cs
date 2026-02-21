namespace ClassroomToolkit.Domain.Timers;

public sealed class TimerEngine
{
    private int _countdownSeconds;
    private int _secondsLeft;
    private int _stopwatchSeconds;
    private int _reminderSeconds;
    private int _reminderCounter;

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
        Running = false;
    }

    public void SetState(TimerMode mode, int countdownSeconds, int secondsLeft, int stopwatchSeconds, bool running)
    {
        Mode = mode;
        _countdownSeconds = Math.Max(0, countdownSeconds);
        _secondsLeft = Math.Max(0, Math.Min(secondsLeft, _countdownSeconds));
        _stopwatchSeconds = Math.Max(0, stopwatchSeconds);
        _reminderCounter = 0;
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
        var totalSeconds = (int)Math.Floor(elapsed.TotalSeconds);
        if (totalSeconds <= 0)
        {
            return;
        }
        if (Mode == TimerMode.Stopwatch)
        {
            _stopwatchSeconds = Math.Max(0, _stopwatchSeconds + totalSeconds);
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
                TimerCompleted?.Invoke();
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
        // Cap triggers to avoid sound storm on large elapsed jumps
        triggers = Math.Min(triggers, 3);
        for (var i = 0; i < triggers; i++)
        {
            ReminderTriggered?.Invoke();
        }
    }
}
