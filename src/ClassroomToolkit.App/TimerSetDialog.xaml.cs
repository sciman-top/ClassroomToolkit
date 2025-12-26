using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace ClassroomToolkit.App;

public partial class TimerSetDialog : Window, INotifyPropertyChanged
{
    private string _minutesText = string.Empty;
    private string _secondsText = string.Empty;

    public TimerSetDialog(int minutes, int seconds)
    {
        InitializeComponent();
        MinutesText = minutes.ToString();
        SecondsText = seconds.ToString();
        DataContext = this;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public int Minutes { get; private set; }

    public int Seconds { get; private set; }

    public string MinutesText
    {
        get => _minutesText;
        set => SetField(ref _minutesText, value);
    }

    public string SecondsText
    {
        get => _secondsText;
        set => SetField(ref _secondsText, value);
    }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(MinutesText, out var minutes) || minutes < 0)
        {
            System.Windows.MessageBox.Show("请输入有效的分钟数。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!int.TryParse(SecondsText, out var seconds) || seconds < 0 || seconds > 59)
        {
            System.Windows.MessageBox.Show("请输入 0-59 的秒数。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        Minutes = Math.Min(minutes, 9999);
        Seconds = seconds;
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
