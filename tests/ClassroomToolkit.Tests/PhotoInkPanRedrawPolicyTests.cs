using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class PhotoInkPanRedrawPolicyTests
{
    [Fact]
    public void ShouldRequest_ShouldReturnFalse_WhenPhotoInkModeInactive()
    {
        var shouldRequest = PhotoInkPanRedrawPolicy.ShouldRequest(
            photoInkModeActive: false,
            currentTranslateX: 120,
            currentTranslateY: 210,
            lastRedrawTranslateX: 100,
            lastRedrawTranslateY: 200);

        shouldRequest.Should().BeFalse();
    }

    [Fact]
    public void ShouldRequest_ShouldReturnFalse_WhenMovementBelowThreshold()
    {
        var shouldRequest = PhotoInkPanRedrawPolicy.ShouldRequest(
            photoInkModeActive: true,
            currentTranslateX: 107,
            currentTranslateY: 200,
            lastRedrawTranslateX: 100,
            lastRedrawTranslateY: 200,
            thresholdDip: 8);

        shouldRequest.Should().BeFalse();
    }

    [Fact]
    public void ShouldRequest_ShouldReturnTrue_WhenMovementReachesThreshold()
    {
        var shouldRequest = PhotoInkPanRedrawPolicy.ShouldRequest(
            photoInkModeActive: true,
            currentTranslateX: 108,
            currentTranslateY: 200,
            lastRedrawTranslateX: 100,
            lastRedrawTranslateY: 200,
            thresholdDip: 8);

        shouldRequest.Should().BeTrue();
    }

    [Fact]
    public void ShouldRequest_ShouldUseResponsiveDefaultThreshold_ForPhotoPan()
    {
        var shouldRequest = PhotoInkPanRedrawPolicy.ShouldRequest(
            photoInkModeActive: true,
            currentTranslateX: 106,
            currentTranslateY: 200,
            lastRedrawTranslateX: 100,
            lastRedrawTranslateY: 200);

        shouldRequest.Should().BeTrue();
    }

    [Fact]
    public void ShouldRequest_ShouldUseResponsiveDefaultThreshold_ForSmallerPhotoPanDelta()
    {
        var shouldRequest = PhotoInkPanRedrawPolicy.ShouldRequest(
            photoInkModeActive: true,
            currentTranslateX: 103.2,
            currentTranslateY: 200,
            lastRedrawTranslateX: 100,
            lastRedrawTranslateY: 200);

        shouldRequest.Should().BeTrue();
    }
}
