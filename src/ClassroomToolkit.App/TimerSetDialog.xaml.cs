using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ClassroomToolkit.App.Helpers;

namespace ClassroomToolkit.App;

public partial class TimerSetDialog : Window
{
    private const int MaxMinutes = 150;
    private bool _updating;
    private DispatcherTimer? _repeatTimer;

    public TimerSetDialog(int minutes, int seconds)
    {
        InitializeComponent();
        SetMinutes(Math.Clamp(minutes, 0, MaxMinutes), updateSlider: true);
        SetSeconds(Math.Clamp(seconds, 0, 59));
        Loaded += (_, _) => WindowPlacementHelper.EnsureVisible(this);
    }

    public int Minutes { get; private set; }

    public int Seconds { get; private set; }

    private void OnMinutesTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_updating)
        {
            return;
        }
        if (!int.TryParse(MinutesBox.Text, out var minutes))
        {
            return;
        }
        minutes = Math.Clamp(minutes, 0, MaxMinutes);
        SetMinutes(minutes, updateSlider: minutes <= 25);
    }

    private void OnMinutesSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updating)
        {
            return;
        }
        SetMinutes((int)Math.Round(e.NewValue), updateSlider: false);
    }

    private void OnMinutesUpClick(object sender, RoutedEventArgs e)
    {
        if (_updating)
        {
            return;
        }
        if (!int.TryParse(MinutesBox.Text, out var minutes))
        {
            minutes = 0;
        }
        minutes = Math.Min(minutes + 1, MaxMinutes);
        SetMinutes(minutes, updateSlider: minutes <= 25);
    }

    private void OnMinutesDownClick(object sender, RoutedEventArgs e)
    {
        if (_updating)
        {
            return;
        }
        if (!int.TryParse(MinutesBox.Text, out var minutes))
        {
            minutes = 0;
        }
        minutes = Math.Max(minutes - 1, 0);
        SetMinutes(minutes, updateSlider: minutes <= 25);
    }

    private void OnMinutesUpMouseDown(object sender, MouseButtonEventArgs e)
    {
        StartRepeatTimer(isIncrement: true);
        e.Handled = true;
    }

    private void OnMinutesDownMouseDown(object sender, MouseButtonEventArgs e)
    {
        StartRepeatTimer(isIncrement: false);
        e.Handled = true;
    }

    private void OnMinutesMouseUp(object sender, MouseButtonEventArgs e)
    {
        StopRepeatTimer();
    }

    private void OnMinutesMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        StopRepeatTimer();
    }

    private void StartRepeatTimer(bool isIncrement)
    {
        StopRepeatTimer();
        _repeatTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300) // 初始延迟 300ms
        };
        int tickCount = 0;
        _repeatTimer.Tick += (_, _) =>
        {
            tickCount++;
            if (tickCount == 1)
            {
                // 首次触发后，加快速度为 100ms 间隔
                _repeatTimer.Interval = TimeSpan.FromMilliseconds(100);
            }
            if (isIncrement)
            {
                OnMinutesUpClick(this, new RoutedEventArgs());
            }
            else
            {
                OnMinutesDownClick(this, new RoutedEventArgs());
            }
        };
        _repeatTimer.Start();
    }

    private void StopRepeatTimer()
    {
        _repeatTimer?.Stop();
        _repeatTimer = null;
    }

    private void OnSecondsTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_updating)
        {
            return;
        }
        if (!int.TryParse(SecondsBox.Text, out var seconds))
        {
            return;
        }
        seconds = Math.Clamp(seconds, 0, 59);
        SetSeconds(seconds);
    }

    private void OnSecondsSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updating)
        {
            return;
        }
        SetSeconds((int)Math.Round(e.NewValue));
    }

    private void SetMinutes(int minutes, bool updateSlider)
    {
        _updating = true;
        MinutesBox.Text = minutes.ToString();
        if (updateSlider)
        {
            MinutesSlider.Value = Math.Min(minutes, 25);
        }
        _updating = false;
    }

    private void SetSeconds(int seconds)
    {
        _updating = true;
        SecondsBox.Text = seconds.ToString();
        SecondsSlider.Value = seconds;
        _updating = false;
    }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(MinutesBox.Text, out var minutes) || minutes < 0 || minutes > MaxMinutes)
        {
            System.Windows.MessageBox.Show($"请输入 0-{MaxMinutes} 的分钟数。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!int.TryParse(SecondsBox.Text, out var seconds) || seconds < 0 || seconds > 59)
        {
            System.Windows.MessageBox.Show("请输入 0-59 的秒数。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        Minutes = minutes;
        Seconds = seconds;
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void OnTitleBarDrag(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
        {
            DragMove();
        }
    }
}
