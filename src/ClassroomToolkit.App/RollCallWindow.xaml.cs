using System.Windows;
using System.Windows.Controls;
using ClassroomToolkit.App.ViewModels;

namespace ClassroomToolkit.App;

public partial class RollCallWindow : Window
{
    private readonly RollCallViewModel _viewModel;

    public RollCallWindow(string dataPath)
    {
        InitializeComponent();
        _viewModel = new RollCallViewModel(dataPath);
        DataContext = _viewModel;
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _viewModel.LoadData();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _viewModel.SaveState();
    }

    private void OnGroupClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string group)
        {
            _viewModel.SetCurrentGroup(group);
        }
    }

    private void OnRollClick(object sender, RoutedEventArgs e)
    {
        _viewModel.RollNext();
    }

    private void OnResetClick(object sender, RoutedEventArgs e)
    {
        _viewModel.ResetCurrentGroup();
    }

    private void OnToggleTimerClick(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("计时功能正在迁移中。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
