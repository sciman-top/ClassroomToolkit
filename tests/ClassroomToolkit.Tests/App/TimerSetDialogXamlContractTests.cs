using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class TimerSetDialogXamlContractTests
{
    [Fact]
    public void TimerSetDialog_ShouldUseSharedDialogShellStyles()
    {
        var xaml = File.ReadAllText(GetTimerSetDialogXamlPath());

        xaml.Should().Contain("Style_DialogShellWindowBorder");
        xaml.Should().Contain("Style_DialogShellTitleBar");
        xaml.Should().Contain("Style_DialogShellActionBar");
    }

    [Fact]
    public void TimerSetDialog_ShouldUseSharedStepperButtonStyle()
    {
        var xaml = File.ReadAllText(GetTimerSetDialogXamlPath());

        xaml.Should().Contain("x:Name=\"MinutesDownButton\"");
        xaml.Should().Contain("x:Name=\"MinutesUpButton\"");
        xaml.Should().Contain("Style_SecondaryButton");
        xaml.Should().NotContain("<Button.Template>", "stepper buttons should reuse shared button style");
    }

    private static string GetTimerSetDialogXamlPath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "TimerSetDialog.xaml");
    }
}
