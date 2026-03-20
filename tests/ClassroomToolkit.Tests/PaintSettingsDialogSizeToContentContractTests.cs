using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PaintSettingsDialogSizeToContentContractTests
{
    [Fact]
    public void OnDialogLoaded_ShouldCommitSizeToContentOnlyAfterDispatcherCallbackRuns()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("var scheduled = PaintActionInvoker.TryInvoke(() =>");
        source.Should().Contain("_sizeToContentCommitted = true;");
        source.Should().Contain("SizeToContent = System.Windows.SizeToContent.Manual;");
        source.Should().Contain("if (!scheduled)");
        source.Should().Contain("if (Dispatcher.CheckAccess())");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintSettingsDialog.xaml.cs");
    }
}
