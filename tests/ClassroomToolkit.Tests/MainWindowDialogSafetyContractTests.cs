using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class MainWindowDialogSafetyContractTests
{
    [Fact]
    public void TryFixWindowBorders_ShouldRunThroughExecuteLifecycleSafe_AndNonFatalCatch()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("private void TryFixWindowBorders(Window window, string phase, string target)");
        source.Should().Contain("ExecuteLifecycleSafe(");
        source.Should().Contain("BorderFixHelper.FixAllBorders(window);");
        source.Should().Contain("catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))");
        source.Should().Contain("BorderFixDiagnosticsPolicy.FormatFailureMessage(");
    }

    [Fact]
    public void TryShowDialogWithDiagnostics_ShouldUseSafeExecutorAndDialogResultStateUpdater()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("private bool TryShowDialogWithDiagnostics(Window dialog, string dialogName)");
        source.Should().Contain("SafeActionExecutionExecutor.TryExecute(");
        source.Should().Contain("DialogShowResultStateUpdater.MarkFromDialogResult(ref result, dialog.SafeShowDialog())");
        source.Should().Contain("DialogShowDiagnosticsPolicy.FormatFailureMessage(");
    }

    [Fact]
    public void ShowMainInfoMessageSafe_ShouldDelegateToLifecycleSafeExecution()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("private void ShowMainInfoMessageSafe(string operation, string message)");
        source.Should().Contain("ExecuteLifecycleSafe(");
        source.Should().Contain("System.Windows.MessageBox.Show(");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "MainWindow.xaml.cs");
    }
}
