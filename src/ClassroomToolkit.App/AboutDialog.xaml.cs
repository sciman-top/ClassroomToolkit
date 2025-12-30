using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using ClassroomToolkit.App.Helpers;

namespace ClassroomToolkit.App;

public partial class AboutDialog : Window
{
    public AboutDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => WindowPlacementHelper.EnsureVisible(this);
    }

    private void OnRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        if (e.Uri != null)
        {
            try
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            }
            catch
            {
                // 忽略无法打开的链接。
            }
        }
        e.Handled = true;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
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
