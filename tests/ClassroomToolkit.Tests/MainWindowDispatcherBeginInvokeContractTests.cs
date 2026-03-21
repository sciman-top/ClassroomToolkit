using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class MainWindowDispatcherBeginInvokeContractTests
{
    [Fact]
    public void TryBeginInvoke_ShouldGuardDispatcherShutdownBeforeBeginInvoke()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)");
        source.Should().Contain("DispatcherBeginInvokeDiagnosticsPolicy.FormatFailureMessage(");
        source.Should().Contain("\"DispatcherShutdown\"");
    }

    [Fact]
    public void TryBeginInvoke_ShouldCatchNonFatalExceptions_AndReturnFalse()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))");
        source.Should().Contain("DispatcherBeginInvokeDiagnosticsPolicy.FormatFailureMessage(");
        source.Should().Contain("return false;");
    }

    [Fact]
    public void TryBeginInvoke_ShouldCallDispatcherBeginInvoke_OnSuccessPath()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("Dispatcher.BeginInvoke(action, priority);");
        source.Should().Contain("return true;");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "MainWindow.xaml.cs");
    }
}
