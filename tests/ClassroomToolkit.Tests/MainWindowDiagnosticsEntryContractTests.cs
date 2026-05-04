using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class MainWindowDiagnosticsEntryContractTests
{
    [Fact]
    public void OnDiagnosticsClick_ShouldUseFullSystemDiagnosticsCollection()
    {
        var source = File.ReadAllText(GetLauncherSourcePath());

        source.Should().Contain("_ = SafeTaskRunner.Run(");
        source.Should().Contain("\"MainWindow.DiagnosticsClick\"");
        source.Should().Contain("Interlocked.CompareExchange(ref _manualDiagnosticsInFlight, 1, 0)");
        source.Should().Contain("Interlocked.Exchange(ref _manualDiagnosticsInFlight, 0)");
        source.Should().Contain("await Dispatcher.InvokeAsync(");
        source.Should().Contain("SystemDiagnostics.CollectSystemDiagnostics(");
        source.Should().Contain("var studentPath = ResolveStudentWorkbookPath();");
        source.Should().Contain("var photoRoot = _settings.InkPhotoRootPath;");
    }

    [Fact]
    public void DiagnosticsDialog_ShouldNotRunRedundantBorderDiagnosticPass()
    {
        var source = File.ReadAllText(TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Diagnostics",
            "DiagnosticsDialog.xaml.cs"));

        source.Should().NotContain("BorderBrushDiagnostic.CheckAllBorders");
        source.Should().NotContain("Console.WriteLine");
    }

    [Fact]
    public void RunStartupDiagnostics_ShouldUseFullSystemDiagnosticsCollection()
    {
        var source = File.ReadAllText(GetLauncherSourcePath());

        source.Should().Contain("_ = SafeTaskRunner.Run(\"MainWindow.StartupDiagnostics\", async token =>");
        source.Should().Contain("var result = SystemDiagnostics.CollectSystemDiagnostics(");
        source.Should().NotContain("SystemDiagnostics.CollectQuickDiagnostics(settingsPath);");
    }

    [Fact]
    public void DiagnosticsDialogFooter_ShouldUseTwoColumnLayout_ToAvoidCopyAndButtonOverlap()
    {
        var xaml = File.ReadAllText(GetDiagnosticsXamlPath());

        xaml.Should().Contain("<Grid.ColumnDefinitions>");
        xaml.Should().Contain("<ColumnDefinition Width=\"*\"/>");
        xaml.Should().Contain("<ColumnDefinition Width=\"Auto\"/>");
        xaml.Should().Contain("Grid.Column=\"0\"");
        xaml.Should().Contain("Grid.Column=\"1\"");
    }

    private static string GetLauncherSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "MainWindow.Launcher.cs");
    }

    private static string GetDiagnosticsXamlPath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Diagnostics",
            "DiagnosticsDialog.xaml");
    }
}
