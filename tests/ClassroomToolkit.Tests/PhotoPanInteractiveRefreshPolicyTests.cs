using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class PhotoPanInteractiveRefreshPolicyTests
{
    [Fact]
    public void ShouldRefresh_ShouldReturnFalse_WhenAccumulatedMovementBelowThreshold()
    {
        var shouldRefresh = PhotoPanInteractiveRefreshPolicy.ShouldRefresh(
            lastRefreshTranslateX: 100,
            lastRefreshTranslateY: 200,
            currentTranslateX: 101.5,
            currentTranslateY: 200,
            thresholdDip: 2.0);

        shouldRefresh.Should().BeFalse();
    }

    [Fact]
    public void ShouldRefresh_ShouldReturnTrue_WhenAccumulatedXMovementReachesThreshold()
    {
        var shouldRefresh = PhotoPanInteractiveRefreshPolicy.ShouldRefresh(
            lastRefreshTranslateX: 100,
            lastRefreshTranslateY: 200,
            currentTranslateX: 102.1,
            currentTranslateY: 200,
            thresholdDip: 2.0);

        shouldRefresh.Should().BeTrue();
    }

    [Fact]
    public void ShouldRefresh_ShouldReturnTrue_WhenAccumulatedYMovementReachesThreshold()
    {
        var shouldRefresh = PhotoPanInteractiveRefreshPolicy.ShouldRefresh(
            lastRefreshTranslateX: 100,
            lastRefreshTranslateY: 200,
            currentTranslateX: 100,
            currentTranslateY: 197.9,
            thresholdDip: 2.0);

        shouldRefresh.Should().BeTrue();
    }
}
