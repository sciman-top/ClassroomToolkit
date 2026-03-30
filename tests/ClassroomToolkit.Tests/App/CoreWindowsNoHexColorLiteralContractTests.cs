using System.Text.RegularExpressions;
using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class CoreWindowsNoHexColorLiteralContractTests
{
    [Fact]
    public void CoreWindows_ShouldAvoidHexColorLiterals()
    {
        var files = new[]
        {
            GetXamlPath("MainWindow.xaml"),
            GetXamlPath("RollCallWindow.xaml"),
            GetXamlPath("AboutDialog.xaml"),
            GetXamlPath("TimerSetDialog.xaml"),
            GetXamlPath("RollCallSettingsDialog.xaml"),
            GetXamlPath("Paint", "PaintSettingsDialog.xaml"),
            GetXamlPath("Paint", "PaintOverlayWindow.xaml"),
            GetXamlPath("Photos", "PhotoOverlayWindow.xaml"),
            GetXamlPath("Photos", "ImageManagerWindow.xaml"),
            GetXamlPath("StudentListDialog.xaml"),
            GetXamlPath("Diagnostics", "DiagnosticsDialog.xaml")
        };

        var hexRegex = new Regex("#[0-9A-Fa-f]{6,8}", RegexOptions.Compiled);
        foreach (var file in files)
        {
            var xaml = File.ReadAllText(file);
            hexRegex.IsMatch(xaml).Should().BeFalse($"{Path.GetFileName(file)} should use semantic color resources");
        }
    }

    private static string GetXamlPath(params string[] segments)
    {
        return TestPathHelper.ResolveAppPath(segments);
    }
}
