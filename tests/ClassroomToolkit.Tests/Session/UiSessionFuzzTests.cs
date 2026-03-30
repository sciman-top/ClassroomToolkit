using System;
using ClassroomToolkit.App.Session;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.Session;

public sealed class UiSessionFuzzTests
{
    [Fact]
    public void RandomEventSequence_ShouldKeepInvariantClean()
    {
        var coordinator = new SessionCoordinator(new NoopEffectRunner());
        var rng = new Random(20260305);

        for (var i = 0; i < 1500; i++)
        {
            coordinator.Dispatch(NextEvent(rng));
            coordinator.LastViolations.Should().BeEmpty($"step={i}, event={coordinator.CurrentState.Scene}/{coordinator.CurrentState.ToolMode}");
            coordinator.CurrentState.FocusOwner.Should().Be(UiSessionFocusOwnerPolicy.Resolve(coordinator.CurrentState.Scene));
            coordinator.CurrentState.NavigationMode.Should().Be(
                UiSessionNavigationPolicy.Resolve(coordinator.CurrentState.Scene, coordinator.CurrentState.ToolMode));
        }
    }

    private static UiSessionEvent NextEvent(Random rng)
    {
        return rng.Next(0, 24) switch
        {
            0 => new EnterPresentationFullscreenEvent(PresentationSourceKind.PowerPoint),
            1 => new EnterPresentationFullscreenEvent(PresentationSourceKind.Wps),
            2 => new ExitPresentationFullscreenEvent(),
            3 => new EnterPhotoFullscreenEvent(PhotoSourceKind.Pdf),
            4 => new EnterPhotoFullscreenEvent(PhotoSourceKind.Image),
            5 => new ExitPhotoFullscreenEvent(),
            6 => new EnterWhiteboardEvent(),
            7 => new ExitWhiteboardEvent(),
            8 => new SwitchToolModeEvent(UiToolMode.Cursor),
            9 => new SwitchToolModeEvent(UiToolMode.Draw),
            10 => new MarkInkDirtyEvent(),
            11 => new MarkInkSavedEvent(),
            12 => new ExitWhiteboardEvent(UiSceneKind.PhotoFullscreen, PhotoSourceKind.Pdf),
            13 => new ExitWhiteboardEvent(
                ResumeScene: UiSceneKind.PresentationFullscreen,
                PresentationSource: PresentationSourceKind.Wps),
            14 => new ExitWhiteboardEvent(
                ResumeScene: UiSceneKind.PresentationFullscreen,
                PresentationSource: PresentationSourceKind.PowerPoint),
            15 => new ExitWhiteboardEvent(UiSceneKind.PhotoFullscreen, PhotoSourceKind.Image),
            16 => new ExitWhiteboardEvent(UiSceneKind.PhotoFullscreen, PhotoSourceKind.Unknown),
            17 => new ExitWhiteboardEvent(UiSceneKind.PresentationFullscreen, PresentationSource: PresentationSourceKind.Unknown),
            18 => new ExitWhiteboardEvent(UiSceneKind.Whiteboard), // unsupported resume scene should gracefully fallback to Idle
            19 => new ExitWhiteboardEvent(UiSceneKind.Idle),
            20 => new EnterPhotoFullscreenEvent(PhotoSourceKind.Unknown),
            21 => new EnterPresentationFullscreenEvent(PresentationSourceKind.Unknown),
            22 => new SwitchToolModeEvent((UiToolMode)rng.Next(0, 2)),
            _ => new ExitWhiteboardEvent()
        };
    }

    private sealed class NoopEffectRunner : IUiSessionEffectRunner
    {
        public void Run(UiSessionTransition transition)
        {
        }
    }
}
