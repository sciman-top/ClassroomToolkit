using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class MainWindowStartupDiagnosticsDispatchContractTests
{
    [Fact]
    public void RunStartupDiagnostics_ShouldFallbackInline_WhenDispatcherSchedulingFailsOnUiThread()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("void ShowDiagnosticsDialog()");
        source.Should().Contain("var scheduled = false;");
        source.Should().Contain("_ = Dispatcher.InvokeAsync(ShowDiagnosticsDialog);");
        source.Should().Contain("if (!scheduled && Dispatcher.CheckAccess())");
        source.Should().Contain("ShowDiagnosticsDialog();");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "MainWindow.Launcher.cs");
    }
}
