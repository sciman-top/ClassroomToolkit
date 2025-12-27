using ClassroomToolkit.Domain.Timers;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class TimerEngineTests
{
    [Fact]
    public void Countdown_ShouldReachZeroAndStop()
    {
        var engine = new TimerEngine();
        engine.SetCountdown(0, 3);
        var completed = false;
        engine.TimerCompleted += () => completed = true;

        engine.Start();
        engine.Tick(TimeSpan.FromSeconds(3));

        engine.SecondsLeft.Should().Be(0);
        engine.Running.Should().BeFalse();
        completed.Should().BeTrue();
    }

    [Fact]
    public void Stopwatch_ShouldIncrement()
    {
        var engine = new TimerEngine();
        engine.SetMode(TimerMode.Stopwatch);
        engine.Start();

        engine.Tick(TimeSpan.FromSeconds(5));

        engine.StopwatchSeconds.Should().Be(5);
    }

    [Fact]
    public void Reminder_ShouldTrigger()
    {
        var engine = new TimerEngine();
        engine.SetCountdown(0, 5);
        engine.ReminderIntervalSeconds = 2;
        var reminders = 0;
        engine.ReminderTriggered += () => reminders++;

        engine.Start();
        engine.Tick(TimeSpan.FromSeconds(5));

        reminders.Should().Be(2);
    }

    [Fact]
    public void Reminder_ShouldNotTriggerOutsideCountdown()
    {
        var engine = new TimerEngine();
        engine.SetMode(TimerMode.Stopwatch);
        engine.ReminderIntervalSeconds = 2;
        var reminders = 0;
        engine.ReminderTriggered += () => reminders++;

        engine.Start();
        engine.Tick(TimeSpan.FromSeconds(5));

        reminders.Should().Be(0);
    }
}
