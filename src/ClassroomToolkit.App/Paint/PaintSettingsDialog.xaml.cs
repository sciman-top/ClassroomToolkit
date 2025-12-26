using System.Windows;
using ClassroomToolkit.App.Settings;

namespace ClassroomToolkit.App.Paint;

public partial class PaintSettingsDialog : Window
{
    public bool ControlMsPpt { get; private set; }
    public bool ControlWpsPpt { get; private set; }
    public string WpsInputMode { get; private set; } = "auto";
    public bool WpsWheelForward { get; private set; }

    public PaintSettingsDialog(AppSettings settings)
    {
        InitializeComponent();
        ControlOfficeCheck.IsChecked = settings.ControlMsPpt;
        ControlWpsCheck.IsChecked = settings.ControlWpsPpt;
        WpsModeCombo.ItemsSource = new[] { "auto", "raw", "message" };
        WpsModeCombo.SelectedItem = settings.WpsInputMode;
        if (WpsModeCombo.SelectedItem == null)
        {
            WpsModeCombo.SelectedIndex = 0;
        }
        WpsWheelCheck.IsChecked = settings.WpsWheelForward;
    }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        ControlMsPpt = ControlOfficeCheck.IsChecked == true;
        ControlWpsPpt = ControlWpsCheck.IsChecked == true;
        WpsInputMode = WpsModeCombo.SelectedItem as string ?? "auto";
        WpsWheelForward = WpsWheelCheck.IsChecked == true;
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
