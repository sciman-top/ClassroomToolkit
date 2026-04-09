using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoSaveNavigationContractTests
{
    [Fact]
    public void SaveCurrentPageOnNavigate_ShouldFinalizeActiveInkOperation_AtMostOncePerCallPath()
    {
        var source = ContractSourceAggregateLoader.LoadByPattern(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Photo.Navigation*.cs");
        var matches = Regex.Matches(source, "FinalizeActiveInkOperation\\(\\);");

        // Keep finalize logic single-entry in SaveCurrentPageOnNavigate to avoid duplicate pointer-release side effects.
        matches.Count.Should().Be(1);
        source.Should().Contain("if (finalizeActiveOperation && hadActiveInkOperation)");
    }
}
