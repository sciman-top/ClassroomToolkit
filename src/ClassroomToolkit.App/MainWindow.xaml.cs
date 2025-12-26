using System.IO;
using System.Windows;
using System.Windows.Input;
using ClassroomToolkit.App.Commands;

namespace ClassroomToolkit.App;

public partial class MainWindow : Window
{
    private RollCallWindow? _rollCallWindow;
    private Paint.PaintOverlayWindow? _overlayWindow;
    private Paint.PaintToolbarWindow? _toolbarWindow;
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
        EnsurePaintWindows();
        if (_overlayWindow == null || _toolbarWindow == null)
        {
            return;
        }
        if (_overlayWindow.IsVisible)
        {
            _overlayWindow.Hide();
            _toolbarWindow.Hide();
        }
        else
        {
            _overlayWindow.Show();
            _toolbarWindow.Show();
            _overlayWindow.Activate();
        }
    }

    private void EnsurePaintWindows()
    {
        if (_overlayWindow != null && _toolbarWindow != null)
        {
            return;
        }
        _overlayWindow = new Paint.PaintOverlayWindow();
        _toolbarWindow = new Paint.PaintToolbarWindow();
        _toolbarWindow.ModeChanged += mode => _overlayWindow.SetMode(mode);
        _toolbarWindow.ShapeTypeChanged += type => _overlayWindow.SetShapeType(type);
        _toolbarWindow.BrushColorChanged += color => _overlayWindow.SetBrush(color, _toolbarWindow.BrushSize, 255);
        _toolbarWindow.BoardColorChanged += color => _overlayWindow.SetBoardColor(color);
        _toolbarWindow.BrushSizeChanged += size => _overlayWindow.SetBrush(_overlayWindow.CurrentBrushColor, size, 255);
        _toolbarWindow.EraserSizeChanged += size => _overlayWindow.SetEraserSize(size);
        _toolbarWindow.ClearRequested += () => _overlayWindow.ClearAll();

        _overlayWindow.SetMode(Paint.PaintToolMode.Brush);
        _overlayWindow.SetBrush(Colors.Red, 12, 255);
        _overlayWindow.SetEraserSize(24);
        _overlayWindow.SetShapeType(Paint.PaintShapeType.Line);
    }

    private void OnOpenSettings()
    {
        MessageBox.Show("设置面板将通过长按触发（迁移中）。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
