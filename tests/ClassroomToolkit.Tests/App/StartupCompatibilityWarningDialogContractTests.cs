using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class StartupCompatibilityWarningDialogContractTests
{
    [Fact]
    public void WarningDialog_ShouldExposeOpenReportAndCopyDiagnosticsActions()
    {
        var xaml = File.ReadAllText(GetXamlPath());
        var codeBehind = File.ReadAllText(GetCodeBehindPath());

        xaml.Should().Contain("x:Name=\"OpenReportButton\"");
        xaml.Should().Contain("x:Name=\"CopyDiagnosticsButton\"");
        xaml.Should().Contain("已切到兼容优先模式。可在兼容诊断恢复提示。");
        codeBehind.Should().Contain("Process.Start(new ProcessStartInfo(_reportPath) { UseShellExecute = true });");
        codeBehind.Should().Contain("System.Windows.Clipboard.SetText(_diagnosticsPayload);");
        codeBehind.Should().Contain("string? diagnosticsPayload = null");
    }

    private static string GetXamlPath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Diagnostics",
            "StartupCompatibilityWarningDialog.xaml");
    }

    private static string GetCodeBehindPath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Diagnostics",
            "StartupCompatibilityWarningDialog.xaml.cs");
    }
}
