using System.Windows;
using ClassroomToolkit.App.Helpers;

namespace ClassroomToolkit.App.Diagnostics;

public partial class StartupCompatibilityWarningDialog : Window
{
    public StartupCompatibilityWarningDialog(string summary, string detail, string suggestion)
    {
        InitializeComponent();
        SummaryText.Text = summary;
        DetailBox.Text = detail;
        SuggestionBox.Text = suggestion;
        Loaded += OnDialogLoaded;
        Closed += OnDialogClosed;
    }

    public bool SuppressCurrentIssues => SuppressCheckBox.IsChecked == true;

    private void OnDialogLoaded(object sender, RoutedEventArgs e)
    {
        WindowPlacementHelper.EnsureVisible(this);
    }

    private void OnDialogClosed(object? sender, EventArgs e)
    {
        Loaded -= OnDialogLoaded;
        Closed -= OnDialogClosed;
    }

    private void OnContinueClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnTitleBarDrag(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
        {
            _ = this.SafeDragMove();
        }
    }
}
