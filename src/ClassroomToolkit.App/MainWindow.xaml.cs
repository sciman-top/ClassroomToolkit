using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ClassroomToolkit.App.Commands;
using ClassroomToolkit.App.Settings;

namespace ClassroomToolkit.App;

public partial class MainWindow : Window
{
    private RollCallWindow? _rollCallWindow;
    private Paint.PaintOverlayWindow? _overlayWindow;
    private Paint.PaintToolbarWindow? _toolbarWindow;
    private readonly AppSettingsService _settingsService;
    private readonly AppSettings _settings;
    public ICommand OpenSettingsCommand { get; }

    public MainWindow()
    {
        InitializeComponent();
        _settingsService = new AppSettingsService(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.ini"));
        _settings = _settingsService.Load();
        OpenSettingsCommand = new RelayCommand(OnOpenSettings);
        DataContext = this;
    }

    private void OnRollCallClick(object sender, RoutedEventArgs e)
    {
        if (_rollCallWindow == null)
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "students.xlsx");
            _rollCallWindow = new RollCallWindow(path, _settingsService, _settings);
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
        _toolbarWindow.ApplySettings(_settings);
        _toolbarWindow.ModeChanged += mode => _overlayWindow.SetMode(mode);
        _toolbarWindow.ShapeTypeChanged += type =>
        {
            _overlayWindow.SetShapeType(type);
            _settings.ShapeType = type;
            SaveSettings();
        };
        _toolbarWindow.BrushColorChanged += color =>
        {
            _overlayWindow.SetBrush(color, _toolbarWindow.BrushSize, _overlayWindow.CurrentBrushOpacity);
            _settings.BrushColor = color;
            SaveSettings();
        };
        _toolbarWindow.BoardColorChanged += color =>
        {
            _overlayWindow.SetBoardColor(color);
            _settings.BoardColor = color;
            SaveSettings();
        };
        _toolbarWindow.BrushSizeChanged += size =>
        {
            _overlayWindow.SetBrush(_overlayWindow.CurrentBrushColor, size, _overlayWindow.CurrentBrushOpacity);
            _settings.BrushSize = size;
            SaveSettings();
        };
        _toolbarWindow.EraserSizeChanged += size =>
        {
            _overlayWindow.SetEraserSize(size);
            _settings.EraserSize = size;
            SaveSettings();
        };
        _toolbarWindow.ClearRequested += () => _overlayWindow.ClearAll();
        _toolbarWindow.UndoRequested += () => _overlayWindow.Undo();
        _toolbarWindow.BrushOpacityChanged += opacity =>
        {
            _overlayWindow.SetBrushOpacity(opacity);
            _settings.BrushOpacity = opacity;
            SaveSettings();
        };
        _toolbarWindow.BoardOpacityChanged += opacity =>
        {
            _overlayWindow.SetBoardOpacity(opacity);
            _settings.BoardOpacity = opacity;
            SaveSettings();
        };
        _toolbarWindow.WpsModeChanged += mode =>
        {
            _overlayWindow.UpdateWpsMode(mode);
            _settings.WpsInputMode = mode;
            SaveSettings();
        };
        _toolbarWindow.WpsWheelMappingChanged += enabled =>
        {
            _overlayWindow.UpdateWpsWheelMapping(enabled);
            _settings.WpsWheelForward = enabled;
            SaveSettings();
        };

        _overlayWindow.SetMode(Paint.PaintToolMode.Brush);
        _overlayWindow.SetBrush(_settings.BrushColor, _settings.BrushSize, _settings.BrushOpacity);
        _overlayWindow.SetEraserSize(_settings.EraserSize);
        _overlayWindow.SetShapeType(_settings.ShapeType);
        _overlayWindow.SetBoardColor(_settings.BoardColor);
        _overlayWindow.SetBoardOpacity(_settings.BoardOpacity);
        _overlayWindow.UpdateWpsMode(_settings.WpsInputMode);
        _overlayWindow.UpdateWpsWheelMapping(_settings.WpsWheelForward);
    }

    private void OnOpenSettings()
    {
        System.Windows.MessageBox.Show("设置面板将通过长按触发（迁移中）。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void SaveSettings()
    {
        _settingsService.Save(_settings);
    }
}
