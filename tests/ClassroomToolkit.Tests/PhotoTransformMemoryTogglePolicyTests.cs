using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoTransformMemoryTogglePolicyTests
{
    [Fact]
    public void ShouldResetUserDirtyState_ShouldReturnTrue_WhenMemoryDisabled()
    {
        PhotoTransformMemoryTogglePolicy.ShouldResetUserDirtyState(rememberPhotoTransform: false)
            .Should()
            .BeTrue();
    }

    [Fact]
    public void ShouldResetUserDirtyState_ShouldReturnFalse_WhenMemoryEnabled()
    {
        PhotoTransformMemoryTogglePolicy.ShouldResetUserDirtyState(rememberPhotoTransform: true)
            .Should()
            .BeFalse();
    }

    [Fact]
    public void ShouldResetUnifiedTransformState_ShouldReturnTrue_WhenMemoryDisabled()
    {
        PhotoTransformMemoryTogglePolicy.ShouldResetUnifiedTransformState(rememberPhotoTransform: false)
            .Should()
            .BeTrue();
    }

    [Fact]
    public void ShouldResetUnifiedTransformState_ShouldReturnFalse_WhenMemoryEnabled()
    {
        PhotoTransformMemoryTogglePolicy.ShouldResetUnifiedTransformState(rememberPhotoTransform: true)
            .Should()
            .BeFalse();
    }
}
