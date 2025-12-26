using System.IO;
using System.Windows;

namespace ClassroomToolkit.App;

public partial class MainWindow : Window
{
    private RollCallWindow? _rollCallWindow;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnRollCallClick(object sender, RoutedEventArgs e)
    {
        if (_rollCallWindow == null)
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "students.xlsx");
            _rollCallWindow = new RollCallWindow(path);
            _rollCallWindow.Closed += (_, _) => _rollCallWindow = null;
        }
        _rollCallWindow.Show();
        _rollCallWindow.Activate();
    }

    private void OnPaintClick(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("画笔功能正在迁移中。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
