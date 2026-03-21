using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageInputSwitchSuccessGateContractTests
{
    [Fact]
    public void TrySwitchActiveImagePageForInput_ShouldResumeOnlyAfterConfirmedSwitch()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("var switchedPage = GetCurrentPageIndexForCrossPage() == targetPage;");
        source.Should().Contain("if (switchedPage)");
        source.Should().Contain("_pendingCrossPageBrushContinuationSample = null;");
        source.Should().Contain("_pendingCrossPageBrushReplayCurrentInput = false;");
        source.Should().Contain("return switchedPage;");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Input.cs");
    }
}
