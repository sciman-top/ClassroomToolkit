using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace ClassroomToolkit.App.Behaviors;

public static class LongPressBehavior
{
    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.RegisterAttached(
            "Command",
            typeof(ICommand),
            typeof(LongPressBehavior),
            new PropertyMetadata(null, OnCommandChanged));

    public static readonly DependencyProperty DurationProperty =
        DependencyProperty.RegisterAttached(
            "Duration",
            typeof(int),
            typeof(LongPressBehavior),
            new PropertyMetadata(700));

    private static readonly DependencyProperty TimerProperty =
        DependencyProperty.RegisterAttached(
            "Timer",
            typeof(LongPressTimerContext),
            typeof(LongPressBehavior),
            new PropertyMetadata(null));

    private static readonly DependencyProperty TriggeredProperty =
        DependencyProperty.RegisterAttached(
            "Triggered",
            typeof(bool),
            typeof(LongPressBehavior),
            new PropertyMetadata(false));

    public static void SetCommand(DependencyObject element, ICommand? value)
        => element.SetValue(CommandProperty, value);

    public static ICommand? GetCommand(DependencyObject element)
        => element.GetValue(CommandProperty) as ICommand;

    public static void SetDuration(DependencyObject element, int value)
        => element.SetValue(DurationProperty, value);

    public static int GetDuration(DependencyObject element)
        => (int)element.GetValue(DurationProperty);

    private static void OnCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UIElement element)
        {
            element.PreviewMouseLeftButtonDown -= OnMouseDown;
            element.PreviewMouseLeftButtonUp -= OnMouseUp;
            element.MouseLeave -= OnMouseLeave;
            if (e.NewValue is ICommand)
            {
                element.PreviewMouseLeftButtonDown += OnMouseDown;
                element.PreviewMouseLeftButtonUp += OnMouseUp;
                element.MouseLeave += OnMouseLeave;
            }
        }
    }

    private static void OnMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not UIElement element)
        {
            return;
        }
        StopTimer(element);
        element.SetValue(TriggeredProperty, false);
        var duration = Math.Max(100, GetDuration(element));
        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(duration)
        };
        EventHandler? tickHandler = null;
        tickHandler = (_, _) =>
        {
            StopTimer(element);
            element.SetValue(TriggeredProperty, true);
            ExecuteCommand(element);
        };
        timer.Tick += tickHandler;
        element.SetValue(TimerProperty, new LongPressTimerContext(timer, tickHandler));
        timer.Start();
    }

    private static void OnMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var element = sender as UIElement;
        var triggered = element != null && (bool)element.GetValue(TriggeredProperty);
        StopTimer(element);
        if (element != null)
        {
            element.SetValue(TriggeredProperty, false);
        }
        if (triggered)
        {
            e.Handled = true;
        }
    }

    private static void OnMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        var element = sender as UIElement;
        StopTimer(element);
        if (element != null)
        {
            element.SetValue(TriggeredProperty, false);
        }
    }

    private static void StopTimer(UIElement? element)
    {
        if (element == null)
        {
            return;
        }
        if (element.GetValue(TimerProperty) is not LongPressTimerContext context)
        {
            return;
        }

        context.Timer.Tick -= context.TickHandler;
        context.Timer.Stop();
        element.ClearValue(TimerProperty);
    }

    private static void ExecuteCommand(UIElement element)
    {
        var command = GetCommand(element);
        if (command == null)
        {
            return;
        }
        if (command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    private sealed record LongPressTimerContext(DispatcherTimer Timer, EventHandler TickHandler);
}
