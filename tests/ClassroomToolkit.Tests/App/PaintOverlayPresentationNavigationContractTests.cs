using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class PaintOverlayPresentationNavigationContractTests
{
    [Fact]
    public void PaintOverlayPresentation_HookPath_ShouldUseIntentParserAndOrchestrator()
    {
        var source = File.ReadAllText(GetPresentationSourcePath());

        source.Should().Contain("PresentationNavigationIntentParser.TryParseHook(direction, source, out var intent)");
        source.Should().Contain("var execution = PresentationNavigationOrchestrator.ResolveHook(");
    }

    [Fact]
    public void PaintOverlayPresentation_HookPath_ShouldUseSourceBasedDispatchPriority_AndHookTaggedOptions()
    {
        var source = File.ReadAllText(GetPresentationSourcePath());

        source.Should().Contain("var dispatchPriority = WpsHookDispatchPriorityPolicy.Resolve(source);");
        source.Should().Contain("var scheduled = TryBeginInvoke(ExecuteHookRequest, dispatchPriority);");
        source.Should().Contain("var options = BuildWpsOptions($\"hook-{source}\");");
    }

    [Fact]
    public void PaintOverlayPresentation_DebounceResolution_ShouldGoThroughPolicy()
    {
        var source = File.ReadAllText(GetPresentationSourcePath());

        source.Should().Contain("return PresentationNavigationDebounceMsPolicy.Resolve(");
        source.Should().Contain("_presentationOptions.WpsDebounceMs,");
        source.Should().Contain("WpsNavDebounceMs);");
    }

    [Fact]
    public void PresentationInputPipeline_HookSource_ShouldDisableServiceDebounce()
    {
        var source = File.ReadAllText(GetInputPipelineSourcePath());

        source.Should().Contain("if (source?.StartsWith(\"hook-\", StringComparison.OrdinalIgnoreCase) == true)");
        source.Should().Contain("return 0;");
    }

    private static string GetPresentationSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Presentation.cs");
    }

    private static string GetInputPipelineSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PresentationInputPipeline.cs");
    }
}
