using System.Windows;
using ClassroomToolkit.App.Helpers;

namespace ClassroomToolkit.App;

public partial class TimerSetDialog : Window
{
    private bool _updating;

    public TimerSetDialog(int minutes, int seconds)
    {
        InitializeComponent();
        SetMinutes(Math.Clamp(minutes, 0, 150), updateSlider: true);
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
        minutes = Math.Clamp(minutes, 0, 150);
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
        if (!int.TryParse(MinutesBox.Text, out var minutes) || minutes < 0 || minutes > 150)
        {
            System.Windows.MessageBox.Show("请输入有效的分钟数。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
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
}
