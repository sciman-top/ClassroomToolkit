using System.IO;
using System.Windows;
using ClassroomToolkit.App.Diagnostics;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.Settings;

namespace ClassroomToolkit.App;

public partial class AutoExitDialog : Window
{
    private readonly AppSettings _settings;

    public AutoExitDialog(int minutes, AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
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

    private void OnDiagnosticClick(object sender, RoutedEventArgs e)
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var settingsPath = Path.Combine(baseDir, "settings.ini");
        var studentPath = StudentResourceLocator.ResolveStudentWorkbookPath();
        var photoRoot = StudentResourceLocator.ResolveStudentPhotoRoot();
        var result = SystemDiagnostics.CollectSystemDiagnostics(_settings, settingsPath, studentPath, photoRoot);
        var dialog = new DiagnosticsDialog(result)
        {
            Owner = this
        };
        dialog.ShowDialog();
    }

    private void OnTitleBarDrag(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
        {
            DragMove();
        }
    }
}
