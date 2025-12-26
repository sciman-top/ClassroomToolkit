using System.Windows;
using ClassroomToolkit.App.Settings;
using ClassroomToolkit.Interop.Presentation;

namespace ClassroomToolkit.App;

public partial class RollCallSettingsDialog : Window
{
    public bool RollCallShowId { get; private set; }
    public bool RollCallShowName { get; private set; }
    public bool RollCallRemoteEnabled { get; private set; }
    public string RemotePresenterKey { get; private set; } = "tab";

    public RollCallSettingsDialog(AppSettings settings)
    {
        InitializeComponent();
        ShowIdCheck.IsChecked = settings.RollCallShowId;
        ShowNameCheck.IsChecked = settings.RollCallShowName;
        RemoteEnabledCheck.IsChecked = settings.RollCallRemoteEnabled;
        RemoteKeyCombo.ItemsSource = new[] { "tab", "shift+b" };
        RemoteKeyCombo.Text = settings.RemotePresenterKey;
        UpdateRemoteKeyEnabled();
    }

    private void OnRemoteEnabledChanged(object sender, RoutedEventArgs e)
    {
        UpdateRemoteKeyEnabled();
    }

    private void UpdateRemoteKeyEnabled()
    {
        var enabled = RemoteEnabledCheck.IsChecked == true;
        RemoteKeyCombo.IsEnabled = enabled;
    }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        var keyText = (RemoteKeyCombo.Text ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(keyText))
        {
            keyText = "tab";
        }
        if (RemoteEnabledCheck.IsChecked == true)
        {
            if (!KeyBindingParser.TryParse(keyText, out var binding) || binding == null)
            {
                MessageBox.Show("请输入有效的按键组合。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            keyText = binding.ToString();
        }
        RollCallShowId = ShowIdCheck.IsChecked == true;
        RollCallShowName = ShowNameCheck.IsChecked == true;
        RollCallRemoteEnabled = RemoteEnabledCheck.IsChecked == true;
        RemotePresenterKey = keyText;
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
