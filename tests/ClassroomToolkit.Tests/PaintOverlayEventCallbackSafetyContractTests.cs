using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PaintOverlayEventCallbackSafetyContractTests
{
    [Fact]
    public void PhotoNavigationAndModeEvents_ShouldBeGuardedBySafeActionExecutor()
    {
        var source = ContractSourceAggregateLoader.LoadByPattern(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Photo.Navigation*.cs");

        source.Should().Contain("SafeActionExecutionExecutor.TryExecute(");
        source.Should().Contain("PhotoModeChanged?.Invoke(true)");
        source.Should().Contain("PhotoModeChanged?.Invoke(false)");
        source.Should().Contain("PhotoNavigationRequested?.Invoke(direction)");
        source.Should().Contain("FloatingZOrderRequested?.Invoke(new FloatingZOrderRequest(plan.ForceAfterDrag))");
    }

    [Fact]
    public void PresentationAndTransformEvents_ShouldBeGuardedBySafeActionExecutor()
    {
        var presentation = File.ReadAllText(GetPresentationSourcePath());
        var transform = ContractSourceAggregateLoader.LoadByPattern(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Photo.Transform*.cs");
        var root = ContractSourceAggregateLoader.LoadByPattern(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow*.cs");

        presentation.Should().Contain("SafeActionExecutionExecutor.TryExecute(");
        presentation.Should().Contain("PresentationForegroundDetected?.Invoke");
        presentation.Should().Contain("PresentationFullscreenDetected?.Invoke");
        transform.Should().Contain("PhotoUnifiedTransformChanged?.Invoke(");
        transform.Should().Contain("SafeActionExecutionExecutor.TryExecute(");
        root.Should().Contain("UiSessionTransitionOccurred?.Invoke(transition)");
        root.Should().Contain("PhotoCursorModeFocusRequested?.Invoke()");
        root.Should().Contain("SafeActionExecutionExecutor.TryExecute(");
    }
    private static string GetPresentationSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Presentation.cs");
    }
}
