using ClassroomToolkit.App.Session;
using FluentAssertions;

namespace ClassroomToolkit.Tests.Session;

public sealed class SessionCoordinatorTests
{
    [Fact]
    public void Dispatch_ShouldThrowArgumentNullException_WhenEventIsNull()
    {
        var coordinator = new SessionCoordinator(new NoopEffectRunner());

        var act = () => coordinator.Dispatch(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Dispatch_ShouldContinue_WhenEffectRunnerThrowsRecoverableException()
    {
        var coordinator = new SessionCoordinator(
            new ThrowingEffectRunner(new InvalidOperationException("recoverable")));

        var transition = coordinator.Dispatch(new EnterWhiteboardEvent());

        transition.Id.Should().Be(1);
        transition.Current.Scene.Should().Be(UiSceneKind.Whiteboard);
        coordinator.CurrentState.Scene.Should().Be(UiSceneKind.Whiteboard);
    }

    [Fact]
    public void Dispatch_ShouldRethrowFatalException_WhenEffectRunnerThrowsFatalException()
    {
        var coordinator = new SessionCoordinator(
            new ThrowingEffectRunner(new BadImageFormatException("fatal")));

        var act = () => coordinator.Dispatch(new EnterWhiteboardEvent());

        act.Should().Throw<BadImageFormatException>();
    }

    private sealed class ThrowingEffectRunner : IUiSessionEffectRunner
    {
        private readonly Exception _exception;

        public ThrowingEffectRunner(Exception exception)
        {
            _exception = exception;
        }

        public void Run(UiSessionTransition transition)
        {
            throw _exception;
        }
    }

    private sealed class NoopEffectRunner : IUiSessionEffectRunner
    {
        public void Run(UiSessionTransition transition)
        {
        }
    }
}
