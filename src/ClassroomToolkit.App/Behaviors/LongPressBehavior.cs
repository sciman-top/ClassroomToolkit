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

    private static readonly DependencyProperty TouchPressActiveProperty =
        DependencyProperty.RegisterAttached(
            "TouchPressActive",
            typeof(bool),
            typeof(LongPressBehavior),
            new PropertyMetadata(false));

    private static readonly DependencyProperty SuppressMousePromotionUntilTicksProperty =
        DependencyProperty.RegisterAttached(
            "SuppressMousePromotionUntilTicks",
            typeof(long),
            typeof(LongPressBehavior),
            new PropertyMetadata(0L));

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
            element.PreviewTouchDown -= OnTouchDown;
            element.PreviewTouchUp -= OnTouchUp;
            element.LostTouchCapture -= OnTouchLostCapture;
            if (e.NewValue is ICommand)
            {
                element.PreviewMouseLeftButtonDown += OnMouseDown;
                element.PreviewMouseLeftButtonUp += OnMouseUp;
                element.MouseLeave += OnMouseLeave;
                element.PreviewTouchDown += OnTouchDown;
                element.PreviewTouchUp += OnTouchUp;
                element.LostTouchCapture += OnTouchLostCapture;
            }
        }
    }

    private static void OnMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not UIElement element)
        {
            return;
        }

        if (ShouldIgnoreMousePromotion(element))
        {
            return;
        }

        StartPressTimer(element);
    }

    private static void OnMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not UIElement element)
        {
            return;
        }

        if (ShouldIgnoreMousePromotion(element))
        {
            return;
        }

        if (CompletePress(element))
        {
            e.Handled = true;
        }
    }

    private static void OnMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is not UIElement element)
        {
            return;
        }

        if (ShouldIgnoreMousePromotion(element))
        {
            return;
        }

        StopPressTimer(element, resetTriggered: true);
    }

    private static void OnTouchDown(object? sender, TouchEventArgs e)
    {
        if (sender is not UIElement element)
        {
            return;
        }

        SetTouchPressActive(element, isActive: true);
        StartPressTimer(element);
        element.CaptureTouch(e.TouchDevice);
    }

    private static void OnTouchUp(object? sender, TouchEventArgs e)
    {
        if (sender is not UIElement element)
        {
            return;
        }

        var handled = CompletePress(element);
        element.ReleaseTouchCapture(e.TouchDevice);
        SetTouchPressActive(element, isActive: false);
        MarkMousePromotionSuppressed(element);
        e.Handled = handled;
    }

    private static void OnTouchLostCapture(object? sender, TouchEventArgs e)
    {
        if (sender is not UIElement element)
        {
            return;
        }

        StopPressTimer(element, resetTriggered: true);
        SetTouchPressActive(element, isActive: false);
        MarkMousePromotionSuppressed(element);
    }

    private static void StartPressTimer(UIElement element)
    {
        StopPressTimer(element);
        element.SetValue(TriggeredProperty, false);
        var duration = Math.Max(100, GetDuration(element));
        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(duration)
        };
        EventHandler? tickHandler = null;
        tickHandler = (_, _) =>
        {
            StopPressTimer(element);
            element.SetValue(TriggeredProperty, true);
            ExecuteCommand(element);
        };
        timer.Tick += tickHandler;
        element.SetValue(TimerProperty, new LongPressTimerContext(timer, tickHandler));
        timer.Start();
    }

    private static bool CompletePress(UIElement? element)
    {
        if (element == null)
        {
            return false;
        }

        var triggered = (bool)element.GetValue(TriggeredProperty);
        StopPressTimer(element);
        element.SetValue(TriggeredProperty, false);
        return triggered;
    }

    private static void StopPressTimer(UIElement? element, bool resetTriggered = false)
    {
        if (element == null)
        {
            return;
        }

        if (element.GetValue(TimerProperty) is LongPressTimerContext context)
        {
            context.Timer.Tick -= context.TickHandler;
            context.Timer.Stop();
            element.ClearValue(TimerProperty);
        }

        if (resetTriggered)
        {
            element.SetValue(TriggeredProperty, false);
        }
    }

    private static bool ShouldIgnoreMousePromotion(UIElement element)
    {
        if ((bool)element.GetValue(TouchPressActiveProperty))
        {
            return true;
        }

        return DateTime.UtcNow.Ticks <= (long)element.GetValue(SuppressMousePromotionUntilTicksProperty);
    }

    private static void SetTouchPressActive(UIElement element, bool isActive)
    {
        element.SetValue(TouchPressActiveProperty, isActive);
        if (isActive)
        {
            element.SetValue(SuppressMousePromotionUntilTicksProperty, 0L);
        }
    }

    private static void MarkMousePromotionSuppressed(UIElement element)
    {
        element.SetValue(
            SuppressMousePromotionUntilTicksProperty,
            DateTime.UtcNow.AddMilliseconds(250).Ticks);
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
