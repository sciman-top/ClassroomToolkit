using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class ComObjectManagerContractTests
{
    [Fact]
    public void Track_ShouldGuardNonComObjects_AndDisposedManager()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("if (!Marshal.IsComObject(comObject))");
        source.Should().Contain("if (_disposed)");
        source.Should().Contain("throw new ObjectDisposedException(nameof(ComObjectManager));");
    }

    [Fact]
    public void Release_ShouldOnlyReleaseTrackedObjects_WithNonFatalGuard()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("var removed = _comObjects.Remove(comObject);");
        source.Should().Contain("removed = _comObjectSet.Remove(comObject) || removed;");
        source.Should().Contain("Marshal.ReleaseComObject(comObject);");
        source.Should().Contain("catch (Exception ex) when (InteropExceptionFilterPolicy.IsNonFatal(ex))");
    }

    [Fact]
    public void Dispose_ShouldReleaseInReverseOrder_AndClearState()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("for (int i = _comObjects.Count - 1; i >= 0; i--)");
        source.Should().Contain("Marshal.ReleaseComObject(_comObjects[i]);");
        source.Should().Contain("_comObjects.Clear();");
        source.Should().Contain("_comObjectSet.Clear();");
    }

    private static string GetSourcePath()
    {
        return Path.Combine(
            FindRepositoryRoot(new DirectoryInfo(AppContext.BaseDirectory))!.FullName,
            "src",
            "ClassroomToolkit.Interop",
            "Utilities",
            "ComObjectManager.cs");
    }

    private static DirectoryInfo? FindRepositoryRoot(DirectoryInfo? start)
    {
        var current = start;
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "ClassroomToolkit.sln")))
            {
                return current;
            }

            current = current.Parent;
        }

        return null;
    }
}
