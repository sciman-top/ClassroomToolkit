using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Navigation;
using ClassroomToolkit.App.Helpers;

namespace ClassroomToolkit.App;

public partial class AboutDialog : Window
{
    public AboutDialog()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            WindowPlacementHelper.EnsureVisible(this);
            VersionText.Text = $"Version {ResolveDisplayVersion()}";
        };
    }

    private void OnRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        if (IsAllowedExternalUri(e.Uri))
        {
            try
            {
                Process.Start(new ProcessStartInfo(e.Uri!.AbsoluteUri) { UseShellExecute = true });
            }
            catch
            {
                // 忽略无法打开的链接。
            }
        }
        e.Handled = true;
    }

    internal static bool IsAllowedExternalUri(Uri? uri)
    {
        if (uri == null || !uri.IsAbsoluteUri)
        {
            return false;
        }

        return uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
               || uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
               || uri.Scheme.Equals(Uri.UriSchemeMailto, StringComparison.OrdinalIgnoreCase);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnCopyInfoClick(object sender, RoutedEventArgs e)
    {
        var payload = string.Join(Environment.NewLine, new[]
        {
            "课堂工具箱",
            VersionText.Text,
            "作者：广州番禺王耀强",
            "公众号：sciman逸居",
            "\"初中物理教研\"Q群：323728546",
            "知乎：https://www.zhihu.com/people/sciman/columns",
            "GitHub：https://github.com/sciman-top/ClassroomTools"
        });

        try
        {
            System.Windows.Clipboard.SetText(payload);
        }
        catch
        {
            // Ignore clipboard failures in locked environments.
        }
    }

    private void OnTitleBarDrag(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
        {
            DragMove();
        }
    }

    private static string ResolveDisplayVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(infoVersion))
        {
            var plusIndex = infoVersion.IndexOf('+');
            return plusIndex > 0 ? infoVersion[..plusIndex] : infoVersion;
        }

        var version = assembly.GetName().Version;
        return version == null ? "未知" : $"{version.Major}.{version.Minor}.{version.Build}";
    }
}
