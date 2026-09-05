using System.Xml.Linq;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

[Trait("Gate", "CoreContract")]
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
            @"src\ClassroomToolkit.App\App.xaml.cs",
            @"src\ClassroomToolkit.App\Startup\AppCompositionRoot.cs"
        };
        var violations = FindTokenUsagesOutsideAllowedFiles("ClassroomToolkit.Infra", allowedFiles);
        violations.Should().BeEmpty("App 层只能在组合根处理 Infra 依赖，其他目录不得直连 Infra 命名空间");
    }

    [Fact]
    public void ApplicationProject_ShouldReferenceOnly_Domain()
    {
        AssertProjectReferences(
            "src/ClassroomToolkit.Application/ClassroomToolkit.Application.csproj",
            "ClassroomToolkit.Domain");
    }

    [Fact]
    public void InfraProject_ShouldReferenceOnly_ApplicationAndDomain()
    {
        AssertProjectReferences(
            "src/ClassroomToolkit.Infra/ClassroomToolkit.Infra.csproj",
            "ClassroomToolkit.Application",
            "ClassroomToolkit.Domain");
    }

    [Fact]
    public void ServicesProject_ShouldReferenceOnly_DomainAndInterop()
    {
        AssertProjectReferences(
            "src/ClassroomToolkit.Services/ClassroomToolkit.Services.csproj",
            "ClassroomToolkit.Domain",
            "ClassroomToolkit.Interop");
    }

    [Fact]
    public void InteropProject_ShouldNotReferenceApplicationLayers()
    {
        AssertProjectReferences("src/ClassroomToolkit.Interop/ClassroomToolkit.Interop.csproj");
    }

    [Fact]
    public void AppProject_ShouldReferenceRuntimeLayersOnly()
    {
        AssertProjectReferences(
            "src/ClassroomToolkit.App/ClassroomToolkit.App.csproj",
            "ClassroomToolkit.Application",
            "ClassroomToolkit.Domain",
            "ClassroomToolkit.Infra",
            "ClassroomToolkit.Interop",
            "ClassroomToolkit.Services");
    }

    [Fact]
    public void AppLayer_ShouldRestrictNativeInteropUsage_ToWindowingDirectory()
    {
        var violations = FindTokenUsagesOutsideAllowedDirectories(
            tokens:
            [
                "Interop.NativeMethods",
                "NativeMethods."
            ],
            allowedDirectoryPrefixes:
            [
                @"src\ClassroomToolkit.App\Windowing\"
            ]);

        violations.Should().BeEmpty("App 层仅允许 Windowing 边界直连 Interop/NativeMethods");
    }

    [Fact]
    public void AppLayer_ShouldKeepPresentationInteropUsage_InsideApprovedSeams()
    {
        var violations = FindTokenUsagesOutsideAllowedDirectories(
            tokens:
            [
                "ClassroomToolkit.Interop.Presentation"
            ],
            allowedDirectoryPrefixes:
            [
                @"src\ClassroomToolkit.App\Windowing\",
                @"src\ClassroomToolkit.App\Paint\"
            ]);

        violations.Should().BeEmpty("Presentation Interop 只能由 Windowing 或 Paint Presentation seam 显式消费");
    }

    [Fact]
    public void AppLayer_ShouldNotLeakInteropThroughGlobalUsings()
    {
        var violations = FindTokenUsagesOutsideAllowedDirectories(
            tokens:
            [
                "global using ClassroomToolkit.Interop"
            ],
            allowedDirectoryPrefixes: []);

        violations.Should().BeEmpty("Interop 依赖必须在文件内显式声明，不能通过 global using 泄漏到整个 App 编译单元");
    }

    [Fact]
    public void AppLayer_ShouldKeepPresentationConstruction_InsideRuntimeFactory()
    {
        var allowedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            @"src\ClassroomToolkit.App\Paint\PaintPresentationRuntimeFactory.cs"
        };
        var constructionTokens = new[]
        {
            "new PresentationControlPlanner(",
            "new PresentationCommandMapper(",
            "new Win32InputSender(",
            "new Win32PresentationResolver(",
            "new PresentationControlService(",
            "new Win32PresentationWindowValidator(",
            "new WpsSlideshowNavigationHook(",
            "new WpsNavHookClient("
        };

        var violations = constructionTokens
            .SelectMany(token => FindTokenUsagesOutsideAllowedFiles(token, allowedFiles))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path)
            .ToArray();

        violations.Should().BeEmpty("Presentation adapter装配必须集中在 PaintPresentationRuntimeFactory，窗口只消费已装配运行时");
    }

    [Fact]
    public void AppRoot_ShouldNotGrowLooseFiles()
    {
        // App 根目录清单锁定：新代码必须进入功能子目录（docs/architecture/feature-template.md）。
        // 删除或移动根目录文件时在同一提交维护本清单；新增根目录文件需要明确理由。
        var expectedRootFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "AboutDialog.xaml.cs",
            "App.xaml.cs",
            "AppGlobalExceptionHandlingPolicy.cs",
            "AppearanceDialog.xaml.cs",
            "AutoExitDialog.xaml.cs",
            "ClassSelectDialog.xaml.cs",
            "DialogShowResultStateUpdater.cs",
            "GlobalSuppressions.cs",
            "IRollCallWindowFactory.cs",
            "InkStartupCleanupLogPolicy.cs",
            "LauncherAutoExitTimerPlanPolicy.cs",
            "LauncherBubbleWindow.xaml.cs",
            "LauncherTopmostVisibilityStateUpdater.cs",
            "LauncherTopmostVisibilityTimestampPolicy.cs",
            "LauncherWindowResolutionPolicy.cs",
            "LauncherWindowRuntimeSelectionLogPolicy.cs",
            "LauncherWorkAreaClampPolicy.cs",
            "MainWindow.Launcher.cs",
            "MainWindow.Lifecycle.cs",
            "MainWindow.Paint.cs",
            "MainWindow.Photo.ImageManager.cs",
            "MainWindow.Photo.cs",
            "MainWindow.RollCall.cs",
            "MainWindow.ZOrder.cs",
            "MainWindow.xaml.cs",
            "MainWindowExitPlanPolicy.cs",
            "MainWindowLoadedToggleActionPolicy.cs",
            "MainWindowOnClosingPlanPolicy.cs",
            "MainWindowOverlayInteractionState.cs",
            "MainWindowOverlayInteractionStatePolicy.cs",
            "MainWindowRuntimeDefaults.cs",
            "MainWindowVisibleChangedPolicy.cs",
            "MainWindowZOrderDedupIntervalPolicy.cs",
            "Program.cs",
            "RemoteHookUnavailableNotificationPolicy.cs",
            "RemoteKeyDialog.xaml.cs",
            "RollCallClickSuppressionPolicy.cs",
            "RollCallRuntimeDefaults.cs",
            "RollCallSettingsDialog.Speech.cs",
            "RollCallSettingsDialog.xaml.cs",
            "RollCallWindow.Input.cs",
            "RollCallWindow.Photo.cs",
            "RollCallWindow.State.cs",
            "RollCallWindow.Timer.cs",
            "RollCallWindow.Windowing.cs",
            "RollCallWindow.xaml.cs",
            "RollCallWindowFactory.cs",
            "SettingsSaveFailureNotificationPolicy.cs",
            "SettingsSaveFailureNotificationStateUpdater.cs",
            "SpeechUnavailableNotificationPolicy.cs",
            "StudentListDialog.xaml.cs",
            "TimerSetDialog.xaml.cs"
        };

        var appRoot = TestPathHelper.ResolveAppPath();
        var actualRootFiles = Directory.GetFiles(appRoot, "*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => Path.GetFileName(path) ?? string.Empty)
            .ToArray();

        var unexpected = actualRootFiles
            .Where(file => !expectedRootFiles.Contains(file))
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var missing = expectedRootFiles
            .Where(file => !actualRootFiles.Contains(file, StringComparer.OrdinalIgnoreCase))
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        unexpected.Should().BeEmpty("App 根目录禁止新增散落文件，新代码进入功能子目录（docs/architecture/feature-template.md）");
        missing.Should().BeEmpty("根目录文件删除/移动后请同步修剪本清单");
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

    private static void AssertProjectReferences(string relativeProjectPath, params string[] expectedProjectNames)
    {
        var actualProjectNames = ReadProjectReferences(relativeProjectPath)
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var expectedNames = expectedProjectNames
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        actualProjectNames.Should().Equal(expectedNames);
    }
}
