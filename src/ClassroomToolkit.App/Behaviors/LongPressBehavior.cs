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
            typeof(DispatcherTimer),
            typeof(LongPressBehavior),
            new PropertyMetadata(null));

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
        var duration = Math.Max(100, GetDuration(element));
        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(duration)
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            ExecuteCommand(element);
        };
        element.SetValue(TimerProperty, timer);
        timer.Start();
    }

    private static void OnMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        StopTimer(sender as UIElement);
    }

    private static void OnMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        StopTimer(sender as UIElement);
    }

    private static void StopTimer(UIElement? element)
    {
        if (element == null)
        {
            return;
        }
        var timer = element.GetValue(TimerProperty) as DispatcherTimer;
        if (timer != null)
        {
            timer.Stop();
        }
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
}
