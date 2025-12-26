using System.IO;
using System.Windows;
using System.Windows.Input;
using ClassroomToolkit.App.Commands;

namespace ClassroomToolkit.App;

public partial class MainWindow : Window
{
    private RollCallWindow? _rollCallWindow;
    public ICommand OpenSettingsCommand { get; }

    public MainWindow()
    {
        InitializeComponent();
        OpenSettingsCommand = new RelayCommand(OnOpenSettings);
        DataContext = this;
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

    private void OnOpenSettings()
    {
        MessageBox.Show("设置面板将通过长按触发（迁移中）。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
