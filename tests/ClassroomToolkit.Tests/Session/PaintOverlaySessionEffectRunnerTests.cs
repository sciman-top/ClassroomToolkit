using ClassroomToolkit.App.Session;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.Session;

public sealed class PaintOverlaySessionEffectRunnerTests
{
    [Fact]
    public void Run_ShouldApplyOverlayTopmost_WhenSceneChangesToNonIdle()
    {
        var overlayCalls = 0;
        var runner = new PaintOverlaySessionEffectRunner(
            applyOverlayTopmost: _ => overlayCalls++,
            applyNavigationMode: _ => { },
            applyInkVisibility: _ => { });

        var previous = UiSessionState.Default;
        var current = UiSessionReducer.Reduce(previous, new EnterPhotoFullscreenEvent(PhotoSourceKind.Image));
        var transition = new UiSessionTransition(1, System.DateTime.UtcNow, new EnterPhotoFullscreenEvent(PhotoSourceKind.Image), previous, current);

        runner.Run(transition);

        overlayCalls.Should().Be(1);
    }

    [Fact]
    public void Run_ShouldApplyNavigationMode_WhenModeChanged()
    {
        UiNavigationMode? captured = null;
        var runner = new PaintOverlaySessionEffectRunner(
            applyOverlayTopmost: _ => { },
            applyNavigationMode: mode => captured = mode,
            applyInkVisibility: _ => { });

        var previous = UiSessionReducer.Reduce(UiSessionState.Default, new EnterPresentationFullscreenEvent(PresentationSourceKind.PowerPoint));
        previous.NavigationMode.Should().Be(UiNavigationMode.HookOnly);
        var current = UiSessionReducer.Reduce(previous, new SwitchToolModeEvent(UiToolMode.Cursor));
        var transition = new UiSessionTransition(2, System.DateTime.UtcNow, new SwitchToolModeEvent(UiToolMode.Cursor), previous, current);

        runner.Run(transition);

        captured.Should().Be(UiNavigationMode.Hybrid);
    }

    [Fact]
    public void Run_ShouldApplyInkVisibility_WhenToolModeChanged()
    {
        UiInkVisibility? captured = null;
        var runner = new PaintOverlaySessionEffectRunner(
            applyOverlayTopmost: _ => { },
            applyNavigationMode: _ => { },
            applyInkVisibility: visibility => captured = visibility);

        var previous = UiSessionReducer.Reduce(UiSessionState.Default, new EnterWhiteboardEvent());
        previous = UiSessionReducer.Reduce(previous, new SwitchToolModeEvent(UiToolMode.Draw));
        var current = UiSessionReducer.Reduce(previous, new SwitchToolModeEvent(UiToolMode.Cursor));
        var transition = new UiSessionTransition(3, System.DateTime.UtcNow, new SwitchToolModeEvent(UiToolMode.Cursor), previous, current);

        runner.Run(transition);

        captured.Should().Be(UiInkVisibility.VisibleReadOnly);
    }

    [Fact]
    public void Run_ShouldApplyWidgetVisibility_WhenFloatingWidgetsVisibilityChanges()
    {
        UiSessionWidgetVisibility? captured = null;
        var runner = new PaintOverlaySessionEffectRunner(
            applyOverlayTopmost: _ => { },
            applyNavigationMode: _ => { },
            applyInkVisibility: _ => { },
            applyWidgetVisibility: visibility => captured = visibility);

        var previous = UiSessionState.Default with
        {
            Scene = UiSceneKind.Idle,
            RollCallVisible = false,
            LauncherVisible = false,
            ToolbarVisible = false
        };
        var current = previous with
        {
            Scene = UiSceneKind.PhotoFullscreen,
            RollCallVisible = true,
            LauncherVisible = true,
            ToolbarVisible = true
        };
        var transition = new UiSessionTransition(
            4,
            System.DateTime.UtcNow,
            new EnterPhotoFullscreenEvent(PhotoSourceKind.Image),
            previous,
            current);

        runner.Run(transition);

        captured.Should().NotBeNull();
        captured!.RollCallVisible.Should().BeTrue();
        captured.LauncherVisible.Should().BeTrue();
        captured.ToolbarVisible.Should().BeTrue();
    }
}
