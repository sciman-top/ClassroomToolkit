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
        PopulateThemePreviews();
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

    /// <summary>Previews read each option's real palette from its color dictionary so they stay in sync with the themes.</summary>
    private void PopulateThemePreviews()
    {
        ApplyThemePreview(MidnightTealPreviewCanvas, MidnightTealPreviewPrimary, AppTheme.MidnightTeal);
        ApplyThemePreview(BlackboardPreviewCanvas, BlackboardPreviewPrimary, AppTheme.Blackboard);
        ApplyThemePreview(LightPreviewCanvas, LightPreviewPrimary, AppTheme.Light);
    }

    private static void ApplyThemePreview(Border canvas, Border accent, AppTheme theme)
    {
        var assemblyName = Uri.EscapeDataString(typeof(AppearanceDialog).Assembly.GetName().Name ?? "ClassroomToolkit.App");
        var dictionary = (ResourceDictionary)System.Windows.Application.LoadComponent(
            new Uri($"/{assemblyName};component/UI/Themes/Colors.{theme}.xaml", UriKind.Relative));

        canvas.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)dictionary["CTK.Color.Canvas"]);
        accent.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)dictionary["CTK.Color.Primary"]);
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

    private void OnRestoreDefaultsClick(object sender, RoutedEventArgs e)
    {
        var defaultTheme = ThemePreferenceService.DefaultTheme;
        SelectRadio(defaultTheme);
        ThemeSelected?.Invoke(defaultTheme);
    }

    private void OnTitleBarDrag(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
        {
            _ = this.SafeDragMove();
        }
    }
}
