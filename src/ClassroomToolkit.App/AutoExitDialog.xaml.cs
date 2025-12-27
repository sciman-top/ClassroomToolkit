using System.Windows;
using ClassroomToolkit.App.Helpers;

namespace ClassroomToolkit.App;

public partial class AutoExitDialog : Window
{
    public AutoExitDialog(int minutes)
    {
        InitializeComponent();
        MinutesBox.Text = Math.Max(0, minutes).ToString();
        MinutesBox.SelectAll();
        MinutesBox.Focus();
        Loaded += (_, _) => WindowPlacementHelper.EnsureVisible(this);
    }

    public int Minutes { get; private set; }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        var text = (MinutesBox.Text ?? string.Empty).Trim();
        if (!int.TryParse(text, out var minutes) || minutes < 0 || minutes > 1440)
        {
            System.Windows.MessageBox.Show("请输入 0-1440 的整数分钟数。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        Minutes = minutes;
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
