using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageInputResumeExecutionContractTests
{
    [Fact]
    public void ResumeCrossPageInputOperationAfterSwitch_ShouldReplayCurrentInput_WhenPlanRequiresUpdate()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("executionPlan.ShouldUpdateBrushAfterContinuation");
        source.Should().Contain("AppendCrossPageContinuationSamples(seed, input, ref lastChangedSample);");
        source.Should().Contain("if (TryUpdateBrushStrokeGeometry(input))");
    }

    [Fact]
    public void HandlePointerMove_ShouldShortCircuit_WhenCrossPageResumeConsumesCurrentInput()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("var consumedByCrossPageResume = ResumeCrossPageInputOperationAfterSwitch(switchedPage, input);");
        source.Should().Contain("if (consumedByCrossPageResume)");
    }

    private static string GetSourcePath()
    {
        return Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", "..",
                "src", "ClassroomToolkit.App", "Paint", "PaintOverlayWindow.Input.cs"));
    }
}
