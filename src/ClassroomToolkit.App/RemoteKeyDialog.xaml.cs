using System.Windows;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.Interop.Presentation;

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
        Loaded += (_, _) => WindowPlacementHelper.EnsureVisible(this);
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
        var text = (CustomBox.Text ?? string.Empty).Trim().ToLowerInvariant();
        if (!KeyBindingParser.TryParse(text, out var binding) || binding == null)
        {
            System.Windows.MessageBox.Show("请输入有效的按键组合。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        SelectedKey = binding.ToString();
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
