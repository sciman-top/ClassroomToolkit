using System.Windows;
using ClassroomToolkit.App.Helpers;

namespace ClassroomToolkit.App.Diagnostics;

public partial class DiagnosticsDialog : Window
{
    private readonly DiagnosticsResult _result;

    public DiagnosticsDialog(DiagnosticsResult result)
    {
        InitializeComponent();
        _result = result;
        Title = result.Title;
        SummaryText.Text = result.Summary;
        DetailBox.Text = result.Detail;
        SuggestionBox.Text = string.IsNullOrWhiteSpace(result.Suggestion) ? "暂无建议。" : result.Suggestion;
        Loaded += (_, _) => WindowPlacementHelper.EnsureVisible(this);
    }

    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        var text = $"{_result.Title}{Environment.NewLine}{_result.Summary}"
                   + $"{Environment.NewLine}{Environment.NewLine}{_result.Detail}";
        if (!string.IsNullOrWhiteSpace(_result.Suggestion))
        {
            text += $"{Environment.NewLine}{Environment.NewLine}{_result.Suggestion}";
        }
        System.Windows.Clipboard.SetText(text);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
