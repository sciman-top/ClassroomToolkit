using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class PhotoLoadedBitmapTransformPathPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnStoredPath_WhenCrossPagePathDisabled()
    {
        var path = PhotoLoadedBitmapTransformPathPolicy.Resolve(
            useCrossPageUnifiedPath: false,
            rememberPhotoTransform: true,
            photoUnifiedTransformReady: true);

        path.Should().Be(PhotoLoadedBitmapTransformPath.TryStoredTransformThenFit);
    }

    [Fact]
    public void Resolve_ShouldReturnUnifiedPath_WhenCrossPageEnabledAndUnifiedReady()
    {
        var path = PhotoLoadedBitmapTransformPathPolicy.Resolve(
            useCrossPageUnifiedPath: true,
            rememberPhotoTransform: true,
            photoUnifiedTransformReady: true);

        path.Should().Be(PhotoLoadedBitmapTransformPath.ApplyUnifiedTransform);
    }

    [Fact]
    public void Resolve_ShouldReturnFitPath_WhenCrossPageEnabledAndUnifiedNotReady()
    {
        var path = PhotoLoadedBitmapTransformPathPolicy.Resolve(
            useCrossPageUnifiedPath: true,
            rememberPhotoTransform: true,
            photoUnifiedTransformReady: false);

        path.Should().Be(PhotoLoadedBitmapTransformPath.FitToViewport);
    }

    [Fact]
    public void Resolve_ShouldReturnFitPath_WhenMemoryDisabledAndCrossPagePathDisabled()
    {
        var path = PhotoLoadedBitmapTransformPathPolicy.Resolve(
            useCrossPageUnifiedPath: false,
            rememberPhotoTransform: false,
            photoUnifiedTransformReady: true);

        path.Should().Be(PhotoLoadedBitmapTransformPath.FitToViewport);
    }

    [Fact]
    public void Resolve_ShouldReturnFitPath_WhenMemoryDisabledEvenWithCrossPageUnifiedReady()
    {
        var path = PhotoLoadedBitmapTransformPathPolicy.Resolve(
            useCrossPageUnifiedPath: true,
            rememberPhotoTransform: false,
            photoUnifiedTransformReady: true);

        path.Should().Be(PhotoLoadedBitmapTransformPath.FitToViewport);
    }
}
