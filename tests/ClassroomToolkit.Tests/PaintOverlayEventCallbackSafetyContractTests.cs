using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PaintOverlayEventCallbackSafetyContractTests
{
    [Fact]
    public void PhotoNavigationAndModeEvents_ShouldBeGuardedBySafeActionExecutor()
    {
        var source = File.ReadAllText(GetPhotoNavigationSourcePath());

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
        var transform = File.ReadAllText(GetTransformSourcePath());
        var root = File.ReadAllText(GetRootSourcePath());

        presentation.Should().Contain("SafeActionExecutionExecutor.TryExecute(");
        presentation.Should().Contain("PresentationForegroundDetected?.Invoke");
        presentation.Should().Contain("PresentationFullscreenDetected?.Invoke");
        transform.Should().Contain("PhotoUnifiedTransformChanged?.Invoke(");
        transform.Should().Contain("SafeActionExecutionExecutor.TryExecute(");
        root.Should().Contain("UiSessionTransitionOccurred?.Invoke(transition)");
        root.Should().Contain("PhotoCursorModeFocusRequested?.Invoke()");
        root.Should().Contain("SafeActionExecutionExecutor.TryExecute(");
    }

    private static string GetPhotoNavigationSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Photo.Navigation.cs");
    }

    private static string GetPresentationSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Presentation.cs");
    }

    private static string GetTransformSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Photo.Transform.cs");
    }

    private static string GetRootSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.xaml.cs");
    }
}
