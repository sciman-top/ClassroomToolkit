using System.Xml.Linq;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class ArchitectureDependencyTests
{
    [Fact]
    public void DomainProject_ShouldNotReference_App_Infra_Interop()
    {
        var references = ReadProjectReferences("src/ClassroomToolkit.Domain/ClassroomToolkit.Domain.csproj");

        references.Should().NotContain(r => r.Contains("ClassroomToolkit.App", StringComparison.OrdinalIgnoreCase));
        references.Should().NotContain(r => r.Contains("ClassroomToolkit.Infra", StringComparison.OrdinalIgnoreCase));
        references.Should().NotContain(r => r.Contains("ClassroomToolkit.Interop", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ApplicationProject_ShouldNotReference_App_Infra_Interop()
    {
        var references = ReadProjectReferences("src/ClassroomToolkit.Application/ClassroomToolkit.Application.csproj");

        references.Should().NotContain(r => r.Contains("ClassroomToolkit.App", StringComparison.OrdinalIgnoreCase));
        references.Should().NotContain(r => r.Contains("ClassroomToolkit.Infra", StringComparison.OrdinalIgnoreCase));
        references.Should().NotContain(r => r.Contains("ClassroomToolkit.Interop", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AppProject_ShouldReference_ApplicationLayer()
    {
        var references = ReadProjectReferences("src/ClassroomToolkit.App/ClassroomToolkit.App.csproj");

        references.Should().Contain(r => r.Contains("ClassroomToolkit.Application", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AppProject_ShouldNotAdd_NewToolkitProjectReferences()
    {
        var references = ReadProjectReferences("src/ClassroomToolkit.App/ClassroomToolkit.App.csproj");
        var toolkitReferences = references
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Where(name => name!.StartsWith("ClassroomToolkit.", StringComparison.OrdinalIgnoreCase))
            .Select(name => name!)
            .ToArray();

        var baselineAllowList = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ClassroomToolkit.Domain",
            "ClassroomToolkit.Application",
            "ClassroomToolkit.Infra",
            "ClassroomToolkit.Services",
            "ClassroomToolkit.Interop"
        };

        var newToolkitReferences = toolkitReferences
            .Where(name => !baselineAllowList.Contains(name))
            .ToArray();
        newToolkitReferences.Should().BeEmpty("当前守卫允许既有 App 项目引用，但不允许新增新的 Toolkit 层级依赖");
    }

    [Fact]
    public void AppLayer_ShouldAvoidInfraNamespace_OutsideCompositionRoot()
    {
        var violations = FindNamespaceUsageViolations(
            namespaceToken: "ClassroomToolkit.Infra",
            excludedFileNames: new[] { "App.xaml.cs" });

        var baselineAllowList = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            @"src\ClassroomToolkit.App\Ink\InkHistoryPersistenceBridge.cs",
            @"src\ClassroomToolkit.App\Paint\PaintOverlayWindow.Export.cs",
            @"src\ClassroomToolkit.App\Paint\PaintWindowFactory.cs"
        };

        var newViolations = violations.Where(v => !baselineAllowList.Contains(v)).ToArray();
        newViolations.Should().BeEmpty("当前守卫允许历史遗留依赖，但不允许新增 Infra 直连");
    }

    [Fact]
    public void AppLayer_ShouldNotAdd_NewInteropNamespaceUsage()
    {
        var violations = FindNamespaceUsageViolations(
            namespaceToken: "ClassroomToolkit.Interop",
            excludedFileNames: Array.Empty<string>());

        var baselineAllowList = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            @"src\ClassroomToolkit.App\Windowing\NativeCursorWindowGeometryInteropAdapter.cs",
            @"src\ClassroomToolkit.App\Windowing\NativeWindowPlacementInteropAdapter.cs",
            @"src\ClassroomToolkit.App\Windowing\NativeWindowStyleInteropAdapter.cs",
            @"src\ClassroomToolkit.App\Windowing\NativeWindowTopmostInteropAdapter.cs",
            @"src\ClassroomToolkit.App\Windowing\PresentationForegroundSuppressionInteropAdapter.cs",
            @"src\ClassroomToolkit.App\Windowing\WindowHandleValidationInteropAdapter.cs",
            @"src\ClassroomToolkit.App\Paint\IWpsNavHookClient.cs",
            @"src\ClassroomToolkit.App\Paint\OverlayPresentationDispatchCoordinator.cs",
            @"src\ClassroomToolkit.App\Paint\OverlayPresentationRouteContextBuilder.cs",
            @"src\ClassroomToolkit.App\Paint\OverlayPresentationTargetSnapshotProvider.cs"
        };

        var newViolations = violations.Where(v => !baselineAllowList.Contains(v)).ToArray();
        newViolations.Should().BeEmpty("当前守卫允许历史遗留 Interop 直连，但不允许继续新增");
    }

    private static IReadOnlyList<string> FindNamespaceUsageViolations(
        string namespaceToken,
        IEnumerable<string> excludedFileNames)
    {
        var excludedSet = new HashSet<string>(excludedFileNames, StringComparer.OrdinalIgnoreCase);
        var appFiles = Directory.GetFiles(TestPathHelper.ResolveAppPath(), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !excludedSet.Contains(Path.GetFileName(path)))
            .ToArray();

        var violations = new List<string>();
        foreach (var file in appFiles)
        {
            var content = File.ReadAllText(file);
            if (content.Contains(namespaceToken, StringComparison.Ordinal))
            {
                violations.Add(TestPathHelper.GetRelativeRepoPath(file));
            }
        }

        return violations;
    }

    private static IReadOnlyList<string> ReadProjectReferences(string relativeProjectPath)
    {
        var fullPath = TestPathHelper.ResolveRepoPath(
            relativeProjectPath
                .Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries));
        var doc = XDocument.Load(fullPath);

        return doc
            .Descendants("ProjectReference")
            .Select(x => x.Attribute("Include")?.Value ?? string.Empty)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();
    }
}
