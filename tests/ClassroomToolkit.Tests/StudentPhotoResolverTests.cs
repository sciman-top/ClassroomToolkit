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
    private static readonly FieldInfo IndexLocksField = typeof(StudentPhotoResolver)
        .GetField("_indexLocks", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo WarmupCancellationField = typeof(StudentPhotoResolver)
        .GetField("_warmupCancellation", BindingFlags.Instance | BindingFlags.NonPublic)!;

    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    public void SanitizeSegment_ShouldRejectDotDirectories(string segment)
    {
        var result = StudentPhotoResolver.SanitizeSegment(segment);

        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData("_1001")]
    [InlineData("1001_")]
    [InlineData("_1001_")]
    [InlineData("__ClassA__")]
    public void SanitizeSegment_ShouldPreserveValidUnderscores(string segment)
    {
        var result = StudentPhotoResolver.SanitizeSegment(segment);

        result.Should().Be(segment);
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
    public void ResolvePhotoPath_ShouldResolve_WhenClassNameAndStudentIdUseValidEdgeUnderscores()
    {
        var rootPath = TestPathHelper.CreateDirectory("ctool_resolver_edge_underscore");
        var className = "__ClassA__";
        var studentId = "_1001_";
        var classDirectory = Path.Combine(rootPath, className);
        Directory.CreateDirectory(classDirectory);
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
    public void ResolvePhotoPath_ShouldNotReturnStalePath_WhenCachedFileWasDeleted()
    {
        var rootPath = TestPathHelper.CreateDirectory("ctool_resolver_stale_deleted");
        var className = "ClassA";
        var classDirectory = Path.Combine(rootPath, className);
        Directory.CreateDirectory(classDirectory);
        var studentId = "1004";
        var target = Path.Combine(classDirectory, $"{studentId}.jpg");
        File.WriteAllBytes(target, new byte[] { 0x01, 0x02, 0x03 });

        try
        {
            var resolver = new StudentPhotoResolver(rootPath);
            resolver.ResolvePhotoPath(className, studentId).Should().Be(target);

            File.Delete(target);

            var result = resolver.ResolvePhotoPath(className, studentId);
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
    public void ResolvePhotoPath_ShouldMergeDirectHitIntoWarmCache_InsteadOfDroppingDirectoryIndex()
    {
        var rootPath = TestPathHelper.CreateDirectory("ctool_resolver_cache_promote");
        var className = "ClassA";
        var classDirectory = Path.Combine(rootPath, className);
        Directory.CreateDirectory(classDirectory);
        var existingStudentId = "1001";
        var newStudentId = "1002";
        var existingPhoto = Path.Combine(classDirectory, $"{existingStudentId}.jpg");
        var newPhoto = Path.Combine(classDirectory, $"{newStudentId}.jpg");
        File.WriteAllBytes(existingPhoto, new byte[] { 0x01, 0x02, 0x03 });

        try
        {
            var resolver = new StudentPhotoResolver(rootPath);

            resolver.ResolvePhotoPath(className, "missing").Should().BeNull();

            File.WriteAllBytes(newPhoto, new byte[] { 0x04, 0x05, 0x06 });
            Directory.SetLastWriteTimeUtc(classDirectory, DateTime.UtcNow.AddSeconds(1));

            resolver.ResolvePhotoPath(className, newStudentId).Should().Be(newPhoto);

            var cache = (System.Collections.IDictionary?)CacheField.GetValue(resolver);
            cache.Should().NotBeNull();
            cache!.Count.Should().Be(1);

            var cacheEntry = cache[classDirectory];
            cacheEntry.Should().NotBeNull();

            var indexProperty = cacheEntry!.GetType().GetProperty("Index", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            indexProperty.Should().NotBeNull();
            var index = indexProperty!.GetValue(cacheEntry) as IReadOnlyDictionary<string, string>;
            index.Should().NotBeNull();
            index![existingStudentId].Should().Be(existingPhoto);
            index[newStudentId].Should().Be(newPhoto);
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
    public void InvalidateStudentCache_ShouldRemoveOnlyTargetStudent_FromWarmCache()
    {
        var rootPath = TestPathHelper.CreateDirectory("ctool_resolver_invalidate_target_only");
        var className = "ClassA";
        var classDirectory = Path.Combine(rootPath, className);
        Directory.CreateDirectory(classDirectory);
        var studentA = "1001";
        var studentB = "1002";
        var photoA = Path.Combine(classDirectory, $"{studentA}.jpg");
        var photoB = Path.Combine(classDirectory, $"{studentB}.jpg");
        File.WriteAllBytes(photoA, new byte[] { 0x01, 0x02, 0x03 });
        File.WriteAllBytes(photoB, new byte[] { 0x04, 0x05, 0x06 });

        try
        {
            var resolver = new StudentPhotoResolver(rootPath);
            resolver.ResolvePhotoPath(className, "missing").Should().BeNull();

            var beforeInvalidate = GetCachedIndex(resolver, classDirectory);
            beforeInvalidate.Should().ContainKey(studentA);
            beforeInvalidate.Should().ContainKey(studentB);

            resolver.InvalidateStudentCache(className, studentA);

            var afterInvalidate = GetCachedIndex(resolver, classDirectory);
            afterInvalidate.Should().NotContainKey(studentA);
            afterInvalidate.Should().ContainKey(studentB);
            resolver.ResolvePhotoPath(className, studentB).Should().Be(photoB);
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
    public void InvalidateStudentCache_ShouldIgnoreInvalidStudentId_WithoutDroppingClassCache()
    {
        var rootPath = TestPathHelper.CreateDirectory("ctool_resolver_invalidate_invalid_id");
        var className = "ClassA";
        var classDirectory = Path.Combine(rootPath, className);
        Directory.CreateDirectory(classDirectory);
        var studentA = "1001";
        var studentB = "1002";
        var photoA = Path.Combine(classDirectory, $"{studentA}.jpg");
        var photoB = Path.Combine(classDirectory, $"{studentB}.jpg");
        File.WriteAllBytes(photoA, new byte[] { 0x01, 0x02, 0x03 });
        File.WriteAllBytes(photoB, new byte[] { 0x04, 0x05, 0x06 });

        try
        {
            var resolver = new StudentPhotoResolver(rootPath);
            resolver.ResolvePhotoPath(className, "missing").Should().BeNull();

            var beforeInvalidate = GetCachedIndex(resolver, classDirectory);
            resolver.InvalidateStudentCache(className, "..");
            var afterInvalidate = GetCachedIndex(resolver, classDirectory);

            afterInvalidate.Should().ContainKey(studentA);
            afterInvalidate.Should().ContainKey(studentB);
            afterInvalidate.Count.Should().Be(beforeInvalidate.Count);
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
    public async Task ResolvePhotoPath_ShouldDetectNewFile_WhenDirectoryWriteTimeDoesNotAdvance()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var rootPath = TestPathHelper.CreateDirectory("ctool_resolver_cache_write_time");
        var className = "ClassA";
        var classDirectory = Path.Combine(rootPath, className);
        Directory.CreateDirectory(classDirectory);
        var studentId = "1003";
        var target = Path.Combine(classDirectory, $"{studentId}.jpg");

        try
        {
            var resolver = new StudentPhotoResolver(rootPath);

            resolver.ResolvePhotoPath(className, studentId).Should().BeNull();

            await Task.Delay(TimeSpan.FromMilliseconds(2200), cancellationToken);
            File.WriteAllBytes(target, new byte[] { 0x01, 0x02, 0x03 });
            Directory.SetLastWriteTimeUtc(classDirectory, DateTime.UtcNow.AddMinutes(-1));

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
        var cancellationToken = TestContext.Current.CancellationToken;
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
                .Select(_ => Task.Run(() => resolver.WarmupCache(), cancellationToken))
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
        var cancellationToken = TestContext.Current.CancellationToken;
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
                }, cancellationToken);
                started.Wait(TimeSpan.FromSeconds(5), cancellationToken).Should().BeTrue();
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
    public void Dispose_ShouldClearIndexLocks_AfterWarmupCache()
    {
        var rootPath = TestPathHelper.CreateDirectory("ctool_resolver_indexlocks");
        var classDirectory = Path.Combine(rootPath, "ClassA");
        Directory.CreateDirectory(classDirectory);
        File.WriteAllBytes(Path.Combine(classDirectory, "1001.jpg"), new byte[] { 0x01, 0x02, 0x03 });

        try
        {
            var resolver = new StudentPhotoResolver(rootPath);
            _ = resolver.ResolvePhotoPath("ClassA", "9999");

            var indexLocksBeforeDispose = (System.Collections.IDictionary?)IndexLocksField.GetValue(resolver);
            indexLocksBeforeDispose.Should().NotBeNull();
            indexLocksBeforeDispose!.Count.Should().BeGreaterThan(0);

            resolver.Dispose();

            var indexLocksAfterDispose = (System.Collections.IDictionary?)IndexLocksField.GetValue(resolver);
            indexLocksAfterDispose.Should().NotBeNull();
            indexLocksAfterDispose!.Count.Should().Be(0);
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
    public void WarmupCache_AfterDispose_ShouldNotCreateNewCancellationTokenSource()
    {
        var rootPath = TestPathHelper.CreateDirectory("ctool_resolver_disposed_warmup");
        try
        {
            var resolver = new StudentPhotoResolver(rootPath);
            resolver.Dispose();

            resolver.WarmupCache(["ClassA"]);

            WarmupCancellationField.GetValue(resolver).Should().BeNull();
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

    private static IReadOnlyDictionary<string, string> GetCachedIndex(StudentPhotoResolver resolver, string directory)
    {
        var cache = (System.Collections.IDictionary?)CacheField.GetValue(resolver);
        cache.Should().NotBeNull();
        cache!.Count.Should().Be(1);
        cache.Contains(directory).Should().BeTrue();

        var cacheEntry = cache[directory];
        cacheEntry.Should().NotBeNull();

        var indexProperty = cacheEntry!.GetType().GetProperty("Index", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        indexProperty.Should().NotBeNull();

        var index = indexProperty!.GetValue(cacheEntry) as IReadOnlyDictionary<string, string>;
        index.Should().NotBeNull();
        return index!;
    }
}
