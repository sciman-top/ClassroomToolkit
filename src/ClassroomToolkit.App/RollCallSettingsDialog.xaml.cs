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
    public bool RollCallShowPhoto { get; private set; }
    public int RollCallPhotoDurationSeconds { get; private set; }
    public string RollCallPhotoSharedClass { get; private set; } = string.Empty;

    public RollCallSettingsDialog(AppSettings settings)
    {
        InitializeComponent();
        ShowIdCheck.IsChecked = settings.RollCallShowId;
        ShowNameCheck.IsChecked = settings.RollCallShowName;
        ShowPhotoCheck.IsChecked = settings.RollCallShowPhoto;
        PhotoDurationSlider.Value = Math.Max(0, Math.Min(10, settings.RollCallPhotoDurationSeconds));
        PhotoSharedBox.Text = settings.RollCallPhotoSharedClass ?? string.Empty;
        RemoteEnabledCheck.IsChecked = settings.RollCallRemoteEnabled;
        RemoteKeyCombo.ItemsSource = new[] { "tab", "shift+b" };
        RemoteKeyCombo.Text = settings.RemotePresenterKey;
        UpdatePhotoDurationLabel();
        UpdatePhotoControls();
        UpdateRemoteKeyEnabled();
    }

    private void OnRemoteEnabledChanged(object sender, RoutedEventArgs e)
    {
        UpdateRemoteKeyEnabled();
    }

    private void OnShowPhotoChanged(object sender, RoutedEventArgs e)
    {
        UpdatePhotoControls();
    }

    private void OnPhotoDurationChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdatePhotoDurationLabel();
    }

    private void UpdateRemoteKeyEnabled()
    {
        var enabled = RemoteEnabledCheck.IsChecked == true;
        RemoteKeyCombo.IsEnabled = enabled;
    }

    private void UpdatePhotoControls()
    {
        var enabled = ShowPhotoCheck.IsChecked == true;
        PhotoDurationSlider.IsEnabled = enabled;
        PhotoSharedBox.IsEnabled = enabled;
    }

    private void UpdatePhotoDurationLabel()
    {
        var seconds = (int)Math.Round(PhotoDurationSlider.Value);
        PhotoDurationLabel.Text = seconds <= 0 ? "不自动关闭" : $"{seconds} 秒";
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
        RollCallShowPhoto = ShowPhotoCheck.IsChecked == true;
        RollCallPhotoDurationSeconds = (int)Math.Round(PhotoDurationSlider.Value);
        RollCallPhotoSharedClass = (PhotoSharedBox.Text ?? string.Empty).Trim();
        RollCallRemoteEnabled = RemoteEnabledCheck.IsChecked == true;
        RemotePresenterKey = keyText;
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
