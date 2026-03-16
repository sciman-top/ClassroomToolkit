using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class OverlayWindowsXamlContractTests
{
    [Fact]
    public void PaintOverlayWindow_ShouldKeepFullscreenShellStructure()
    {
        var xaml = File.ReadAllText(GetPaintOverlayWindowXamlPath());

        xaml.Should().Contain("x:Name=\"PhotoControlLayer\"");
        xaml.Should().Contain("Style_FullscreenShellCloseButton");
        xaml.Should().Contain("Style_FullscreenShellSideRail");
        xaml.Should().Contain("Style_FullscreenShellHintBadge");
    }

    [Fact]
    public void OverlayWindows_ShouldAvoidInlineDropShadowEffects()
    {
        var paintXaml = File.ReadAllText(GetPaintOverlayWindowXamlPath());
        var photoXaml = File.ReadAllText(GetPhotoOverlayWindowXamlPath());

        paintXaml.Should().NotContain("<DropShadowEffect");
        photoXaml.Should().NotContain("<DropShadowEffect");
    }

    [Fact]
    public void OverlayWindows_ShouldUseSharedShadowAndOverlayTokens()
    {
        var paintXaml = File.ReadAllText(GetPaintOverlayWindowXamlPath());
        var photoXaml = File.ReadAllText(GetPhotoOverlayWindowXamlPath());

        paintXaml.Should().Contain("Shadow_Card_Subtle");
        paintXaml.Should().Contain("Brush_OverlayMask");
        paintXaml.Should().Contain("Brush_Surface_Primary");
        paintXaml.Should().Contain("Brush_Border_Subtle");

        photoXaml.Should().Contain("Shadow_Floating");
        photoXaml.Should().Contain("Shadow_Card_Subtle");
        photoXaml.Should().Contain("Style_FullscreenShellHintBadge");
    }

    [Fact]
    public void FloatingWindows_ShouldReuseSharedBubbleAndPaletteStyles()
    {
        var toolbarXaml = File.ReadAllText(GetXamlPath("Paint", "PaintToolbarWindow.xaml"));
        var paletteXaml = File.ReadAllText(GetXamlPath("Paint", "QuickColorPaletteWindow.xaml"));
        var paletteCode = File.ReadAllText(GetSourcePath("Paint", "QuickColorPaletteWindow.xaml.cs"));
        var bubbleXaml = File.ReadAllText(GetXamlPath("LauncherBubbleWindow.xaml"));
        var groupOverlayXaml = File.ReadAllText(GetXamlPath("Photos", "RollCallGroupOverlayWindow.xaml"));

        toolbarXaml.Should().Contain("Style_ColorBubbleToggle");
        toolbarXaml.Should().NotContain("x:Key=\"Style_ColorBubble\"");
        paletteCode.Should().Contain("Style_ColorPaletteButton");
        paletteXaml.Should().NotContain("x:Key=\"ColorBlockButtonStyle\"");
        bubbleXaml.Should().Contain("Style_BubbleShellRoot");
        groupOverlayXaml.Should().Contain("Style_BubbleShellRoot");
    }

    private static string GetPaintOverlayWindowXamlPath()
    {
        return Path.Combine(
            FindRepositoryRoot(new DirectoryInfo(AppContext.BaseDirectory))!.FullName,
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.xaml");
    }

    private static string GetPhotoOverlayWindowXamlPath()
    {
        return Path.Combine(
            FindRepositoryRoot(new DirectoryInfo(AppContext.BaseDirectory))!.FullName,
            "src",
            "ClassroomToolkit.App",
            "Photos",
            "PhotoOverlayWindow.xaml");
    }

    private static string GetXamlPath(params string[] segments)
    {
        var root = FindRepositoryRoot(new DirectoryInfo(AppContext.BaseDirectory))!.FullName;
        var full = new List<string> { root, "src", "ClassroomToolkit.App" };
        full.AddRange(segments);
        return Path.Combine(full.ToArray());
    }

    private static string GetSourcePath(params string[] segments)
    {
        var root = FindRepositoryRoot(new DirectoryInfo(AppContext.BaseDirectory))!.FullName;
        var full = new List<string> { root, "src", "ClassroomToolkit.App" };
        full.AddRange(segments);
        return Path.Combine(full.ToArray());
    }

    private static DirectoryInfo? FindRepositoryRoot(DirectoryInfo? start)
    {
        var current = start;
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "ClassroomToolkit.sln")))
            {
                return current;
            }

            current = current.Parent;
        }

        return null;
    }
}
