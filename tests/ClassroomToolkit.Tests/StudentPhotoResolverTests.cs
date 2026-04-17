using ClassroomToolkit.App.Photos;
using FluentAssertions;
using System.Reflection;

namespace ClassroomToolkit.Tests;

public sealed class StudentPhotoResolverTests
{
    private static readonly MethodInfo GetIndexMethod = typeof(StudentPhotoResolver)
        .GetMethod("GetIndex", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly MethodInfo GetIndexLockMethod = typeof(StudentPhotoResolver)
        .GetMethod("GetIndexLock", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo CacheField = typeof(StudentPhotoResolver)
        .GetField("_cache", BindingFlags.Instance | BindingFlags.NonPublic)!;

    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    public void SanitizeSegment_ShouldRejectDotDirectories(string segment)
    {
        var result = StudentPhotoResolver.SanitizeSegment(segment);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ResolvePhotoPath_ShouldNotTraverseOutsideRoot_WhenClassNameIsParentDirectory()
    {
        var rootPath = TestPathHelper.CreateDirectory("ctool_resolver");
        var parentPath = Directory.GetParent(rootPath)!.FullName;
        var outsidePhoto = Path.Combine(parentPath, $"student_{Guid.NewGuid():N}.jpg");
        Directory.CreateDirectory(rootPath);
        File.WriteAllBytes(outsidePhoto, new byte[] { 0x01, 0x02, 0x03 });

        try
        {
            var studentId = Path.GetFileNameWithoutExtension(outsidePhoto);
            var resolver = new StudentPhotoResolver(rootPath);

            var result = resolver.ResolvePhotoPath("..", studentId);

            result.Should().BeNull();
        }
        finally
        {
            if (File.Exists(outsidePhoto))
            {
                File.Delete(outsidePhoto);
            }

            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }

    [Fact]
    public void ResolvePhotoPath_ShouldNotTraverseOutsideClassDirectory_WhenStudentIdContainsParentPath()
    {
        var rootPath = TestPathHelper.CreateDirectory("ctool_resolver_sid");
        var className = "ClassA";
        var classDirectory = Path.Combine(rootPath, className);
        var outsidePhotoName = $"student_{Guid.NewGuid():N}";
        var outsidePhoto = Path.Combine(rootPath, $"{outsidePhotoName}.jpg");
        Directory.CreateDirectory(classDirectory);
        File.WriteAllBytes(outsidePhoto, new byte[] { 0x01, 0x02, 0x03 });

        try
        {
            var resolver = new StudentPhotoResolver(rootPath);

            var result = resolver.ResolvePhotoPath(className, $"..\\{outsidePhotoName}");

            result.Should().BeNull();
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }

    [Fact]
    public void ResolvePhotoPath_ShouldResolveInClassDirectory_WhenStudentIdIsValid()
    {
        var rootPath = TestPathHelper.CreateDirectory("ctool_resolver_ok");
        var className = "ClassA";
        var classDirectory = Path.Combine(rootPath, className);
        Directory.CreateDirectory(classDirectory);
        var studentId = "1001";
        var target = Path.Combine(classDirectory, $"{studentId}.jpg");
        File.WriteAllBytes(target, new byte[] { 0x01, 0x02, 0x03 });

        try
        {
            var resolver = new StudentPhotoResolver(rootPath);

            var result = resolver.ResolvePhotoPath(className, studentId);

            result.Should().Be(target);
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }

    [Fact]
    public void ResolvePhotoPath_ShouldDetectNewFile_WhenWarmCacheMisses()
    {
        var rootPath = TestPathHelper.CreateDirectory("ctool_resolver_cache_refresh");
        var className = "ClassA";
        var classDirectory = Path.Combine(rootPath, className);
        Directory.CreateDirectory(classDirectory);
        var studentId = "1002";
        var target = Path.Combine(classDirectory, $"{studentId}.jpg");

        try
        {
            var resolver = new StudentPhotoResolver(rootPath);

            resolver.ResolvePhotoPath(className, studentId).Should().BeNull();
            File.WriteAllBytes(target, new byte[] { 0x01, 0x02, 0x03 });

            var result = resolver.ResolvePhotoPath(className, studentId);

            result.Should().Be(target);
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }

    [Fact]
    public void Dispose_ShouldBeIdempotent_AndAllowSafeCallsAfterDispose()
    {
        var rootPath = TestPathHelper.CreateDirectory("ctool_resolver_dispose");
        var resolver = new StudentPhotoResolver(rootPath);

        resolver.Dispose();
        resolver.Dispose();

        var act = () =>
        {
            resolver.WarmupCache(["ClassA"]);
            var path = resolver.ResolvePhotoPath("ClassA", "1001");
            resolver.InvalidateStudentCache("ClassA", "1001");
            path.Should().BeNull();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public async Task Dispose_ShouldBeSafe_DuringConcurrentWarmup()
    {
        var rootPath = TestPathHelper.CreateDirectory("ctool_resolver_concurrent");
        try
        {
            for (var i = 0; i < 8; i++)
            {
                var classDir = Path.Combine(rootPath, $"Class{i}");
                Directory.CreateDirectory(classDir);
                File.WriteAllBytes(Path.Combine(classDir, "1001.jpg"), new byte[] { 0x01, 0x02, 0x03 });
            }

            var resolver = new StudentPhotoResolver(rootPath);
            var warmupTasks = Enumerable.Range(0, 16)
                .Select(_ => Task.Run(() => resolver.WarmupCache()))
                .ToArray();

            var act = async () =>
            {
                resolver.Dispose();
                resolver.Dispose();
                await Task.WhenAll(warmupTasks);
            };

            await act.Should().NotThrowAsync();
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Dispose_ShouldNotRepopulateCache_WhenIndexBuildResumesAfterDispose()
    {
        var rootPath = TestPathHelper.CreateDirectory("ctool_resolver_dispose_race");
        var classDirectory = Path.Combine(rootPath, "ClassA");
        Directory.CreateDirectory(classDirectory);
        File.WriteAllBytes(Path.Combine(classDirectory, "1001.jpg"), new byte[] { 0x01, 0x02, 0x03 });

        try
        {
            var resolver = new StudentPhotoResolver(rootPath);
            var indexLock = GetIndexLockMethod.Invoke(resolver, new object[] { classDirectory });
            indexLock.Should().NotBeNull();
            using var started = new ManualResetEventSlim(false);

            Task buildTask;
            lock (indexLock!)
            {
                buildTask = Task.Run(() =>
                {
                    started.Set();
                    return GetIndexMethod.Invoke(resolver, new object[] { classDirectory });
                });
                started.Wait(TimeSpan.FromSeconds(1)).Should().BeTrue();
                resolver.Dispose();
            }

            await buildTask;

            var cache = CacheField.GetValue(resolver);
            cache.Should().NotBeNull();
            ((System.Collections.IDictionary)cache!).Count.Should().Be(0);
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }

    [Fact]
    public void Resolver_ShouldUseIgnoreInaccessibleEnumeration_ForWarmupAndIndex()
    {
        var source = File.ReadAllText(GetResolverSourcePath());

        source.Should().Contain("IgnoreInaccessible = true");
        source.Should().Contain("Directory.EnumerateDirectories(_rootPath, \"*\", TopLevelIgnoreInaccessibleOptions)");
        source.Should().Contain("Directory.EnumerateFiles(directory, \"*\", TopLevelIgnoreInaccessibleOptions)");
    }

    private static string GetResolverSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Photos",
            "StudentPhotoResolver.cs");
    }
}
