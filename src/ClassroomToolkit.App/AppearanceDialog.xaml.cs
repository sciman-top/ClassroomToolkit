using System.Windows;
using System.Windows.Controls;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.UI.Themes;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App;

public partial class AppearanceDialog : Window
{
    private bool _suppressThemeNotification;

    public AppearanceDialog(string? theme = null)
    {
        InitializeComponent();
        SelectRadio(ThemePreferenceService.Parse(theme));
        Loaded += OnDialogLoaded;
        Closed += OnDialogClosed;
    }

    /// <summary>Receives each user-selected theme so the caller can apply it immediately.</summary>
    public event Action<AppTheme>? ThemeSelected;

    private AppTheme SelectedTheme =>
        Enum.TryParse<AppTheme>(SelectedRadio()?.Tag as string, out var theme) ? theme : ThemePreferenceService.DefaultTheme;

    private void SelectRadio(AppTheme theme)
    {
        _suppressThemeNotification = true;
        try
        {
            (theme switch
            {
                AppTheme.Blackboard => BlackboardRadio,
                AppTheme.Light => LightRadio,
                _ => MidnightTealRadio
            }).IsChecked = true;
        }
        finally
        {
            _suppressThemeNotification = false;
        }
    }

    private System.Windows.Controls.RadioButton? SelectedRadio()
    {
        if (BlackboardRadio.IsChecked == true)
        {
            return BlackboardRadio;
        }

        return LightRadio.IsChecked == true ? LightRadio : MidnightTealRadio;
    }

    private void OnThemeChecked(object sender, RoutedEventArgs e)
    {
        if (_suppressThemeNotification)
        {
            return;
        }

        ThemeSelected?.Invoke(SelectedTheme);
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

    private void OnClose(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void OnTitleBarDrag(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
        {
            _ = this.SafeDragMove();
        }
    }
}
