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
        _countdownSeconds = Math.Max(0, minutes * 60 + seconds);
        _secondsLeft = _countdownSeconds;
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
        Running = true;
    }

    public void Pause()
    {
        Running = false;
    }

    public void Toggle()
    {
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
        for (var i = 0; i < totalSeconds; i++)
        {
            TickOneSecond();
        }
    }

    private void TickOneSecond()
    {
        if (Mode == TimerMode.Countdown)
        {
            if (_secondsLeft > 0)
            {
                _secondsLeft--;
                TriggerReminder();
                if (_secondsLeft == 0)
                {
                    Running = false;
                    TimerCompleted?.Invoke();
                }
            }
        }
        else if (Mode == TimerMode.Stopwatch)
        {
            _stopwatchSeconds++;
        }
    }

    private void TriggerReminder()
    {
        if (_reminderSeconds <= 0 || Mode != TimerMode.Countdown)
        {
            return;
        }
        _reminderCounter++;
        if (_reminderCounter >= _reminderSeconds)
        {
            _reminderCounter = 0;
            ReminderTriggered?.Invoke();
        }
    }
}
