using System.Linq;
using ClassroomToolkit.App.Session;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.Session;

public sealed class UiSessionInvariantTests
{
    [Fact]
    public void Coordinator_ShouldKeepInvariantClean_AcrossTypicalSceneTransitions()
    {
        var coordinator = new SessionCoordinator(new NoopEffectRunner());

        coordinator.Dispatch(new SwitchToolModeEvent(UiToolMode.Cursor));
        coordinator.Dispatch(new EnterPresentationFullscreenEvent(PresentationSourceKind.PowerPoint));
        coordinator.LastViolations.Should().BeEmpty();

        coordinator.Dispatch(new EnterPhotoFullscreenEvent(PhotoSourceKind.Pdf));
        coordinator.LastViolations.Should().BeEmpty();

        coordinator.Dispatch(new EnterWhiteboardEvent());
        coordinator.LastViolations.Should().BeEmpty();

        coordinator.Dispatch(new ExitWhiteboardEvent());
        coordinator.LastViolations.Should().BeEmpty();

        coordinator.Dispatch(new ExitPhotoFullscreenEvent());
        coordinator.Dispatch(new ExitPresentationFullscreenEvent());
        coordinator.LastViolations.Should().BeEmpty();
    }

    [Fact]
    public void Validate_ShouldReportViolations_ForInvalidManualState()
    {
        var invalid = new UiSessionState(
            Scene: UiSceneKind.PresentationFullscreen,
            ToolMode: UiToolMode.Draw,
            NavigationMode: UiNavigationMode.Hybrid,
            FocusOwner: UiFocusOwner.Photo,
            InkVisibility: UiInkVisibility.Hidden,
            InkDirty: false,
            OverlayTopmostRequired: false,
            RollCallVisible: false,
            LauncherVisible: false,
            ToolbarVisible: false);

        var violations = UiSessionInvariants.Validate(invalid);

        violations.Should().NotBeEmpty();
        violations.Any(v => v.Contains("INV-001")).Should().BeTrue();
        violations.Any(v => v.Contains("INV-002")).Should().BeTrue();
        violations.Any(v => v.Contains("INV-003")).Should().BeTrue();
        violations.Any(v => v.Contains("INV-007")).Should().BeTrue();
    }

    private sealed class NoopEffectRunner : IUiSessionEffectRunner
    {
        public void Run(UiSessionTransition transition)
        {
        }
    }
}
