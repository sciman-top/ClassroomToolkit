using System.Windows;
using System.Windows.Input;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.Services.Input;

namespace ClassroomToolkit.App;

public partial class RemoteKeyDialog : Window
{
    public string SelectedKey { get; private set; } = "tab";

    public RemoteKeyDialog(string current)
    {
        InitializeComponent();
        PresetCombo.ItemsSource = new[] { "tab", "shift+b", "自定义" };
        PresetCombo.SelectedIndex = 0;
        CustomBox.Text = current;
        if (string.Equals(current, "tab", StringComparison.OrdinalIgnoreCase))
        {
            PresetCombo.SelectedIndex = 0;
        }
        else if (string.Equals(current, "shift+b", StringComparison.OrdinalIgnoreCase))
        {
            PresetCombo.SelectedIndex = 1;
        }
        else
        {
            PresetCombo.SelectedIndex = 2;
        }
        Loaded += OnDialogLoaded;
        Closed += OnDialogClosed;
    }

    private void OnDialogLoaded(object sender, RoutedEventArgs e)
    {
        WindowPlacementHelper.EnsureVisible(this);
    }

    private void OnDialogClosed(object? sender, EventArgs e)
    {
        Loaded -= OnDialogLoaded;
        Closed -= OnDialogClosed;
    }

    private void OnPresetChanged(object sender, RoutedEventArgs e)
    {
        if (PresetCombo.SelectedItem is string preset)
        {
            if (preset == "tab")
            {
                CustomBox.Text = "tab";
            }
            else if (preset == "shift+b")
            {
                CustomBox.Text = "shift+b";
            }
        }
    }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        if (!KeyBindingTokenParser.TryNormalize(CustomBox.Text, out var normalized))
        {
            System.Windows.MessageBox.Show("请输入有效的按键组合。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SelectedKey = normalized;
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void OnTitleBarDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            _ = this.SafeDragMove();
        }
    }
}
