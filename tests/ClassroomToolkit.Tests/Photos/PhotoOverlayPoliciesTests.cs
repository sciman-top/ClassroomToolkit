using AwesomeAssertions;
using ClassroomToolkit.App.Photos;

namespace ClassroomToolkit.Tests.Photos;

public sealed class PhotoCursorModeFocusPolicyTests
{
    [Fact]
    public void ShouldFocusOverlay_ShouldReturnTrue_WhenPhotoModeActive()
    {
        PhotoOverlayPolicies.ShouldFocusOverlay(photoModeActive: true).Should().BeTrue();
    }

    [Fact]
    public void ShouldFocusOverlay_ShouldReturnFalse_WhenPhotoModeInactive()
    {
        PhotoOverlayPolicies.ShouldFocusOverlay(photoModeActive: false).Should().BeFalse();
    }
}

public sealed class PhotoModeOwnerSyncPolicyTests
{
    [Fact]
    public void ShouldSyncOwners_ShouldReturnTrue_WhenNotTouchingPhotoFullscreenSurface()
    {
        PhotoOverlayPolicies.ShouldSyncOwners(touchPhotoFullscreenSurface: false).Should().BeTrue();
    }

    [Fact]
    public void ShouldSyncOwners_ShouldReturnFalse_WhenTouchingPhotoFullscreenSurface()
    {
        PhotoOverlayPolicies.ShouldSyncOwners(touchPhotoFullscreenSurface: true).Should().BeFalse();
    }
}

public sealed class PhotoShowInkOverlayChangePolicyTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    public void ShouldApply_ShouldMatchExpected(bool currentEnabled, bool nextEnabled, bool expected)
    {
        PhotoOverlayPolicies.ShouldApply(currentEnabled, nextEnabled)
            .Should()
            .Be(expected);
    }
}

public sealed class PhotoUnifiedTransformChangePolicyTests
{
    [Fact]
    public void HasChanged_ShouldReturnTrue_WhenUnifiedTransformDisabled()
    {
        var changed = PhotoOverlayPolicies.HasChanged(
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
        var changed = PhotoOverlayPolicies.HasChanged(
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
        var changed = PhotoOverlayPolicies.HasChanged(
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

public sealed class StudentPhotoCachePolicyTests
{
    [Fact]
    public void ShouldReuseCache_ShouldReturnTrue_WhenWithinTtl()
    {
        var nowUtc = new DateTime(2026, 3, 7, 4, 0, 0, DateTimeKind.Utc);
        var reuse = PhotoOverlayPolicies.ShouldReuseCache(
            nowUtc,
            nowUtc.AddMinutes(-5),
            TimeSpan.FromMinutes(10));

        reuse.Should().BeTrue();
    }

    [Fact]
    public void ShouldReuseCache_ShouldReturnFalse_WhenTtlExpired()
    {
        var nowUtc = new DateTime(2026, 3, 7, 4, 0, 0, DateTimeKind.Utc);
        var reuse = PhotoOverlayPolicies.ShouldReuseCache(
            nowUtc,
            nowUtc.AddMinutes(-11),
            TimeSpan.FromMinutes(10));

        reuse.Should().BeFalse();
    }
}
