using ClassroomToolkit.App.Photos;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class StudentPhotoResolverTests
{
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
        var rootPath = Path.Combine(Path.GetTempPath(), $"ctool_resolver_{Guid.NewGuid():N}");
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
        var rootPath = Path.Combine(Path.GetTempPath(), $"ctool_resolver_sid_{Guid.NewGuid():N}");
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
        var rootPath = Path.Combine(Path.GetTempPath(), $"ctool_resolver_ok_{Guid.NewGuid():N}");
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
}
