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
    public void AppLayer_ShouldRestrictInfraUsage_ToCompositionRoot()
    {
        var allowedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            @"src\ClassroomToolkit.App\App.xaml.cs"
        };
        var violations = FindTokenUsagesOutsideAllowedFiles("ClassroomToolkit.Infra", allowedFiles);
        violations.Should().BeEmpty("App 层只能在组合根处理 Infra 依赖，其他目录不得直连 Infra 命名空间");
    }

    [Fact]
    public void AppLayer_ShouldRestrictInteropUsage_ToWindowingDirectory()
    {
        var violations = FindTokenUsagesOutsideAllowedDirectories(
            tokens:
            [
                "ClassroomToolkit.Interop",
                "Interop.NativeMethods",
                "NativeMethods."
            ],
            allowedDirectoryPrefixes:
            [
                @"src\ClassroomToolkit.App\Windowing\"
            ]);

        violations.Should().BeEmpty("App 层仅允许 Windowing 边界直连 Interop/NativeMethods");
    }

    private static IReadOnlyList<string> FindTokenUsagesOutsideAllowedFiles(
        string token,
        ISet<string> allowedRelativeFiles)
    {
        var violations = new List<string>();
        foreach (var file in EnumerateAppSourceFiles())
        {
            var content = File.ReadAllText(file);
            if (!content.Contains(token, StringComparison.Ordinal))
            {
                continue;
            }

            var relativePath = NormalizePath(TestPathHelper.GetRelativeRepoPath(file));
            if (allowedRelativeFiles.Contains(relativePath))
            {
                continue;
            }

            violations.Add(relativePath);
        }

        return violations;
    }

    private static IReadOnlyList<string> FindTokenUsagesOutsideAllowedDirectories(
        IReadOnlyList<string> tokens,
        IReadOnlyList<string> allowedDirectoryPrefixes)
    {
        var normalizedAllowedPrefixes = allowedDirectoryPrefixes
            .Select(NormalizePath)
            .Select(path => path.EndsWith("\\", StringComparison.Ordinal) ? path : $"{path}\\")
            .ToArray();

        var violations = new List<string>();
        foreach (var file in EnumerateAppSourceFiles())
        {
            var content = File.ReadAllText(file);
            if (!tokens.Any(token => content.Contains(token, StringComparison.Ordinal)))
            {
                continue;
            }

            var relativePath = NormalizePath(TestPathHelper.GetRelativeRepoPath(file));
            var insideAllowedDirectory = normalizedAllowedPrefixes.Any(prefix =>
                relativePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            if (!insideAllowedDirectory)
            {
                violations.Add(relativePath);
            }
        }

        return violations.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(path => path).ToArray();
    }

    private static IEnumerable<string> EnumerateAppSourceFiles()
    {
        var appRoot = TestPathHelper.ResolveAppPath();
        return Directory.GetFiles(appRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildArtifactPath(path))
            .ToArray();
    }

    private static bool IsBuildArtifactPath(string fullPath)
    {
        var parts = fullPath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        return parts.Any(part =>
            part.Equals("obj", StringComparison.OrdinalIgnoreCase)
            || part.Equals("bin", StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('/', '\\');
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
