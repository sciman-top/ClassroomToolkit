using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class DialogTouchFlowContractTests
{
    [Fact]
    public void TimerAndAutoExitDialogs_ShouldAvoidMouseOnlyRepeat_AndForcedKeyboardFocus()
    {
        var timerXaml = File.ReadAllText(GetTimerXamlPath());
        var timerSource = File.ReadAllText(GetTimerSourcePath());
        var autoExitSource = File.ReadAllText(GetAutoExitSourcePath());

        timerXaml.Should().Contain("PreviewTouchDown=\"OnMinutesDownTouchDown\"");
        timerXaml.Should().Contain("PreviewTouchDown=\"OnMinutesUpTouchDown\"");
        timerSource.Should().Contain("private void OnMinutesTouchUpOrLostCapture");
        autoExitSource.Should().NotContain("TryKeyboardFocus(MinutesBox, shouldFocus: true)");
    }

    [Fact]
    public void PhotoOverlay_ShouldAvoidOverlayCloseButton_AndCloseOnImageTap()
    {
        var xaml = File.ReadAllText(GetPhotoOverlayXamlPath());

        xaml.Should().NotContain("x:Name=\"CloseButton\"");
        xaml.Should().NotContain("Style_OverlayShellCloseButton");
        xaml.Should().NotContain("Click=\"OnCloseClick\"");
        xaml.Should().NotContain("MouseLeftButtonDown=\"OnCloseClick\"");
        xaml.Should().Contain("MouseLeftButtonDown=\"OnPhotoImageMouseLeftButtonDown\"");
    }

    private static string GetTimerXamlPath() => TestPathHelper.ResolveRepoPath(
        "src",
        "ClassroomToolkit.App",
        "TimerSetDialog.xaml");

    private static string GetTimerSourcePath() => TestPathHelper.ResolveRepoPath(
        "src",
        "ClassroomToolkit.App",
        "TimerSetDialog.xaml.cs");

    private static string GetAutoExitSourcePath() => TestPathHelper.ResolveRepoPath(
        "src",
        "ClassroomToolkit.App",
        "AutoExitDialog.xaml.cs");

    private static string GetPhotoOverlayXamlPath() => TestPathHelper.ResolveRepoPath(
        "src",
        "ClassroomToolkit.App",
        "Photos",
        "PhotoOverlayWindow.xaml");
}
