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

        return string.Concat(coreSource, "\n", lifecycleSource);
    }
}
