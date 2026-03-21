using ClassroomToolkit.App.Paint;
using ClassroomToolkit.Interop.Presentation;
using FluentAssertions;
using ClassroomToolkit.App.Session;

namespace ClassroomToolkit.Tests;

public sealed class PresentationNavigationModelsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(-2)]
    public void TryCreatePageTurn_ShouldReturnFalse_WhenDirectionIsNotStep(int direction)
    {
        var created = PresentationNavigationIntent.TryCreatePageTurn(
            direction,
            PresentationNavigationSource.HookKeyboard,
            out var intent);

        created.Should().BeFalse();
        intent.Should().Be(PresentationNavigationIntent.None);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void TryCreatePageTurn_ShouldNormalizeAndReturnIntent_WhenDirectionIsStep(int direction)
    {
        var created = PresentationNavigationIntent.TryCreatePageTurn(
            direction,
            PresentationNavigationSource.OverlayKeyboard,
            out var intent);

        created.Should().BeTrue();
        intent.Direction.Should().Be(direction);
        intent.Source.Should().Be(PresentationNavigationSource.OverlayKeyboard);
        intent.IsKeyboardSource.Should().BeTrue();
        intent.IsWheelSource.Should().BeFalse();
    }

    [Fact]
    public void DefaultSnapshot_ShouldMatchDeterministicSafeDefaults()
    {
        var snapshot = PresentationNavigationContextSnapshot.Default;

        snapshot.Mode.Should().Be(PaintToolMode.Brush);
        snapshot.NavigationMode.Should().Be(UiNavigationMode.Disabled);
        snapshot.ForegroundPresentationType.Should().Be(PresentationType.None);
        snapshot.CurrentPresentationType.Should().Be(PresentationType.None);
        snapshot.AllowWps.Should().BeFalse();
        snapshot.AllowOffice.Should().BeFalse();
        snapshot.PresentationFullscreenActive.Should().BeFalse();
    }

    [Fact]
    public void DispatchFactory_ShouldCreateDispatchDecision()
    {
        var decision = PresentationNavigationDecision.Dispatch(
            strategy: InputStrategy.Message,
            suppressAsDebounced: false,
            reason: "ok");

        decision.ShouldDispatch.Should().BeTrue();
        decision.ShouldSuppressAsDebounced.Should().BeFalse();
        decision.Strategy.Should().Be(InputStrategy.Message);
        decision.Reason.Should().Be("ok");
    }

    [Fact]
    public void SuppressFactory_ShouldCreateSuppressedDecision()
    {
        var decision = PresentationNavigationDecision.SuppressAsDebounced("debounced");

        decision.ShouldDispatch.Should().BeFalse();
        decision.ShouldSuppressAsDebounced.Should().BeTrue();
        decision.Reason.Should().Be("debounced");
    }
}
