using System.Globalization;
using System.Windows;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.Windowing;
using ClassroomToolkit.App.UI.Themes;

namespace ClassroomToolkit.App;

public partial class AutoExitDialog : Window
{
    public AutoExitDialog(int minutes, string? theme = null)
    {
        InitializeComponent();
        MinutesBox.Text = Math.Max(0, minutes).ToString(CultureInfo.InvariantCulture);
        ThemeCombo.SelectedValue = ThemePreferenceService.Parse(theme).ToString();
        MinutesBox.SelectAll();
        Loaded += OnDialogLoaded;
        Closed += OnDialogClosed;
    }

    public int Minutes { get; private set; }

    public string SelectedTheme { get; private set; } = ThemePreferenceService.DefaultTheme.ToString();

    private void OnDialogLoaded(object sender, RoutedEventArgs e)
    {
        WindowPlacementHelper.EnsureVisible(this);
    }

    private void OnDialogClosed(object? sender, EventArgs e)
    {
        Loaded -= OnDialogLoaded;
        Closed -= OnDialogClosed;
    }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        var text = (MinutesBox.Text ?? string.Empty).Trim();
        if (!int.TryParse(text, out var minutes) || minutes < 0 || minutes > 1440)
        {
            System.Windows.MessageBox.Show("请输入 0-1440 的整数分钟数。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        Minutes = minutes;
        SelectedTheme = ThemePreferenceService.Normalize(ThemeCombo.SelectedValue as string);
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
            _ = this.SafeDragMove();
        }
    }
}
