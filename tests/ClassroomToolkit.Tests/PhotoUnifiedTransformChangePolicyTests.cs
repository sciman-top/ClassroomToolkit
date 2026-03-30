using ClassroomToolkit.App.Photos;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoUnifiedTransformChangePolicyTests
{
    [Fact]
    public void HasChanged_ShouldReturnTrue_WhenUnifiedTransformDisabled()
    {
        var changed = PhotoUnifiedTransformChangePolicy.HasChanged(
            unifiedTransformEnabled: false,
            currentScaleX: 1,
            currentScaleY: 1,
            currentTranslateX: 0,
            currentTranslateY: 0,
            nextScaleX: 1,
            nextScaleY: 1,
            nextTranslateX: 0,
            nextTranslateY: 0,
            epsilon: 0.0001);

        changed.Should().BeTrue();
    }

    [Fact]
    public void HasChanged_ShouldReturnFalse_WhenAllValuesWithinEpsilon()
    {
        var changed = PhotoUnifiedTransformChangePolicy.HasChanged(
            unifiedTransformEnabled: true,
            currentScaleX: 1,
            currentScaleY: 1,
            currentTranslateX: 5,
            currentTranslateY: 10,
            nextScaleX: 1.00001,
            nextScaleY: 1.00002,
            nextTranslateX: 5.00003,
            nextTranslateY: 10.00004,
            epsilon: 0.001);

        changed.Should().BeFalse();
    }

    [Fact]
    public void HasChanged_ShouldReturnTrue_WhenAnyValueExceedsEpsilon()
    {
        var changed = PhotoUnifiedTransformChangePolicy.HasChanged(
            unifiedTransformEnabled: true,
            currentScaleX: 1,
            currentScaleY: 1,
            currentTranslateX: 5,
            currentTranslateY: 10,
            nextScaleX: 1.01,
            nextScaleY: 1,
            nextTranslateX: 5,
            nextTranslateY: 10,
            epsilon: 0.001);

        changed.Should().BeTrue();
    }
}
