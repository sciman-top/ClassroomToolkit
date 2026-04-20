using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class LauncherBubbleTouchContractTests
{
    [Fact]
    public void LauncherBubble_ShouldKeepSmallVisualBody_ButSupportTouchInput()
    {
        var xaml = File.ReadAllText(GetXamlPath());
        var source = File.ReadAllText(GetSourcePath());

        xaml.Should().Contain("Width=\"64\" Height=\"64\"");
        xaml.Should().Contain("Width=\"42\" Height=\"42\"");
        source.Should().Contain("TouchDown += OnTouchDown;");
        source.Should().Contain("TouchMove += OnTouchMove;");
        source.Should().Contain("TouchUp += OnTouchUp;");
        source.Should().Contain("LostTouchCapture += OnLostTouchCapture;");
    }

    private static string GetXamlPath() => TestPathHelper.ResolveRepoPath(
        "src",
        "ClassroomToolkit.App",
        "LauncherBubbleWindow.xaml");

    private static string GetSourcePath() => TestPathHelper.ResolveRepoPath(
        "src",
        "ClassroomToolkit.App",
        "LauncherBubbleWindow.xaml.cs");
}
