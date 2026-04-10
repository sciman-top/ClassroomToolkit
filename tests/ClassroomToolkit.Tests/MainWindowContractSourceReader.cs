using System.IO;

namespace ClassroomToolkit.Tests;

internal static class MainWindowContractSourceReader
{
    public static string ReadCombinedSource()
    {
        var coreSource = File.ReadAllText(TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "MainWindow.xaml.cs"));
        var lifecycleSource = File.ReadAllText(TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "MainWindow.Lifecycle.cs"));
        var zOrderSource = File.ReadAllText(TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "MainWindow.ZOrder.cs"));
        var rollCallSource = File.ReadAllText(TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "MainWindow.RollCall.cs"));

        return string.Concat(coreSource, "\n", lifecycleSource, "\n", zOrderSource, "\n", rollCallSource);
    }
}
