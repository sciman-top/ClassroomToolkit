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
        
        // 在构造函数中立即修复 BorderBrush 问题
        try
        {
            BorderFixHelper.FixAllBorders(this);
            System.Diagnostics.Debug.WriteLine("DiagnosticsDialog: 构造函数中修复完成");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DiagnosticsDialog 构造函数修复失败: {ex.Message}");
        }
        
        Loaded += (_, _) => 
        {
            WindowPlacementHelper.EnsureVisible(this);
            
            // 再次诊断 BorderBrush 问题
            try
            {
                BorderBrushDiagnostic.CheckAllBorders(this);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"BorderBrush 诊断失败: {ex.Message}");
            }
        };
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
        // 直接关闭窗口，不设置 DialogResult
        // 调用方会通过 SafeShowDialog 的返回值知道结果
        Close();
    }

    private void OnTitleBarDrag(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
        {
            DragMove();
        }
    }
}
