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
}
