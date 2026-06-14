using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class MainWindowPhotoFocusDispatchContractTests
{
    [Fact]
    public void FocusOverlayForPhotoNavigation_ShouldFallbackInlineOnlyOnUiThread_WhenDispatchFails()
    {
        var source = ContractSourceAggregateLoader.LoadByPattern(
            "src",
            "ClassroomToolkit.App",
            "MainWindow.Photo*.cs");

        source.Should().Contain("var scheduled = TryBeginInvoke(");
        source.Should().Contain("if (!scheduled)");
        source.Should().Contain("if (Dispatcher.CheckAccess())");
        source.Should().Contain("FocusNow();");
    }
}
