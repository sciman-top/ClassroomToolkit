using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class AppGlobalExceptionDialogDispatchContractTests
{
    [Fact]
    public void HandleGlobalException_ShouldFallbackInline_WhenDispatcherSchedulingFailsOnUiThread()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("void ShowGlobalErrorDialog()");
        source.Should().Contain("_ = Dispatcher.InvokeAsync(ShowGlobalErrorDialog);");
        source.Should().Contain("if (!scheduled && Dispatcher.CheckAccess())");
        source.Should().Contain("ShowGlobalErrorDialog();");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "App.xaml.cs");
    }
}
