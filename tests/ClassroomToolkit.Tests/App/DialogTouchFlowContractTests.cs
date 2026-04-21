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
    public void CompactTouchDialogs_ShouldUseUnifiedTouchSizedActions()
    {
        var timerXaml = File.ReadAllText(GetTimerXamlPath());
        var rollCallXaml = File.ReadAllText(GetRollCallXamlPath());

        timerXaml.Should().Contain("Style_TimerCompactStepperButton");
        timerXaml.Should().Contain("Style_TimerPresetButton");
        timerXaml.Should().Contain("Style_TimerValueTextBox");
        rollCallXaml.Should().Contain("Style_RollCallBottomBarTextButton");
        rollCallXaml.Should().Contain("Style_RollCallBottomBarAccentButton");
        rollCallXaml.Should().Contain("Style_RollCallGroupButton");
        rollCallXaml.Should().Contain("<Setter Property=\"MinWidth\" Value=\"48\"/>");
        rollCallXaml.Should().NotContain("MinWidth=\"44\" Height=\"{StaticResource Size_Button_Action_Height_Compact}\"");
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

    [Fact]
    public void ScrollableTouchScreens_ShouldEnableVerticalFirstPanning()
    {
        var paintSettingsXaml = File.ReadAllText(GetPaintSettingsXamlPath());
        var classSelectXaml = File.ReadAllText(GetClassSelectXamlPath());
        var studentListXaml = File.ReadAllText(GetStudentListXamlPath());
        var imageManagerXaml = File.ReadAllText(GetImageManagerXamlPath());

        paintSettingsXaml.Should().Contain("PanningMode=\"VerticalFirst\"");
        classSelectXaml.Should().Contain("ScrollViewer.PanningMode=\"VerticalFirst\"");
        studentListXaml.Should().Contain("PanningMode=\"VerticalFirst\"");
        imageManagerXaml.Should().Contain("ScrollViewer.PanningMode=\"VerticalFirst\"");
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

    private static string GetRollCallXamlPath() => TestPathHelper.ResolveRepoPath(
        "src",
        "ClassroomToolkit.App",
        "RollCallWindow.xaml");

    private static string GetPaintSettingsXamlPath() => TestPathHelper.ResolveRepoPath(
        "src",
        "ClassroomToolkit.App",
        "Paint",
        "PaintSettingsDialog.xaml");

    private static string GetClassSelectXamlPath() => TestPathHelper.ResolveRepoPath(
        "src",
        "ClassroomToolkit.App",
        "ClassSelectDialog.xaml");

    private static string GetStudentListXamlPath() => TestPathHelper.ResolveRepoPath(
        "src",
        "ClassroomToolkit.App",
        "StudentListDialog.xaml");

    private static string GetImageManagerXamlPath() => TestPathHelper.ResolveRepoPath(
        "src",
        "ClassroomToolkit.App",
        "Photos",
        "ImageManagerWindow.xaml");
}
