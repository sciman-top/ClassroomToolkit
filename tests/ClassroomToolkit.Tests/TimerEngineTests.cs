using ClassroomToolkit.Domain.Timers;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class TimerEngineTests
{
    [Fact]
    public void SetCountdown_ShouldClampToIntMax_WhenInputWouldOverflow()
    {
        var engine = new TimerEngine();

        engine.SetCountdown(int.MaxValue, 59);

        engine.CountdownSeconds.Should().Be(int.MaxValue);
        engine.SecondsLeft.Should().Be(int.MaxValue);
    }

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

    [Fact]
    public void ClockMode_ShouldNotEnterRunningState()
    {
        var engine = new TimerEngine();
        engine.SetMode(TimerMode.Clock);

        engine.Start();
        engine.Running.Should().BeFalse();

        engine.Toggle();
        engine.Running.Should().BeFalse();
    }

    [Fact]
    public void Countdown_ShouldNotThrow_WhenTimerCompletedSubscriberFails()
    {
        var engine = new TimerEngine();
        engine.SetCountdown(0, 1);
        var safeHandlerCalled = false;
        engine.TimerCompleted += () => throw new InvalidOperationException("boom");
        engine.TimerCompleted += () => safeHandlerCalled = true;

        var act = () =>
        {
            engine.Start();
            engine.Tick(TimeSpan.FromSeconds(1));
        };

        act.Should().NotThrow();
        engine.Running.Should().BeFalse();
        engine.SecondsLeft.Should().Be(0);
        safeHandlerCalled.Should().BeTrue();
    }

    [Fact]
    public void Reminder_ShouldNotThrow_WhenReminderSubscriberFails()
    {
        var engine = new TimerEngine();
        engine.SetCountdown(0, 5);
        engine.ReminderIntervalSeconds = 2;
        var safeHandlerCount = 0;
        engine.ReminderTriggered += () => throw new InvalidOperationException("boom");
        engine.ReminderTriggered += () => safeHandlerCount++;

        var act = () =>
        {
            engine.Start();
            engine.Tick(TimeSpan.FromSeconds(5));
        };

        act.Should().NotThrow();
        safeHandlerCount.Should().Be(2);
    }

    [Fact]
    public void Reminder_ShouldNotTrigger_AtCountdownCompletionBoundary()
    {
        var engine = new TimerEngine();
        engine.SetCountdown(0, 5);
        engine.ReminderIntervalSeconds = 5;
        var reminders = 0;
        var completed = false;
        engine.ReminderTriggered += () => reminders++;
        engine.TimerCompleted += () => completed = true;

        engine.Start();
        engine.Tick(TimeSpan.FromSeconds(5));

        reminders.Should().Be(0);
        completed.Should().BeTrue();
        engine.SecondsLeft.Should().Be(0);
    }

    [Fact]
    public void Reminder_ShouldKeepOnlyPreCompletionTriggers_WhenTickReachesCompletion()
    {
        var engine = new TimerEngine();
        engine.SetCountdown(0, 10);
        engine.ReminderIntervalSeconds = 5;
        var reminders = 0;
        engine.ReminderTriggered += () => reminders++;

        engine.Start();
        engine.Tick(TimeSpan.FromSeconds(10));

        reminders.Should().Be(1);
        engine.SecondsLeft.Should().Be(0);
    }
}
