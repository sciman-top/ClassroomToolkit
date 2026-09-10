using AwesomeAssertions;
using ClassroomToolkit.Interop.Presentation;

namespace ClassroomToolkit.Tests;

public sealed class PresentationTargetSelectionTests
{
    [Fact]
    public void SelectBestTarget_ShouldReturnOnlyValidCandidate()
    {
        var wps = BuildTarget(100, "wpspresentation.exe");
        var office = BuildTarget(200, "powerpnt.exe");

        Win32PresentationResolver.SelectBestTarget(wps, -1, PresentationTarget.Empty, -1)
            .Should().Be(wps);
        Win32PresentationResolver.SelectBestTarget(PresentationTarget.Empty, -1, office, -1)
            .Should().Be(office);
    }

    [Fact]
    public void SelectBestTarget_ShouldReturnHigherScore()
    {
        var wps = BuildTarget(300, "wpspresentation.exe");
        var office = BuildTarget(400, "powerpnt.exe");

        Win32PresentationResolver.SelectBestTarget(wps, 10, office, 20)
            .Should().Be(office);
        Win32PresentationResolver.SelectBestTarget(wps, 20, office, 10)
            .Should().Be(wps);
    }

    [Fact]
    public void SelectBestTarget_ShouldFailClosedWhenScoresTie()
    {
        var wps = BuildTarget(500, "wpspresentation.exe");
        var office = BuildTarget(600, "powerpnt.exe");

        Win32PresentationResolver.SelectBestTarget(wps, 15, office, 15)
            .Should().Be(PresentationTarget.Empty);
    }

    private static PresentationTarget BuildTarget(long hwnd, string processName)
    {
        return new PresentationTarget(
            new IntPtr(hwnd),
            new PresentationWindowInfo(1, processName, ["showclass"]));
    }
}
