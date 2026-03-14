using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPagePointerUpImmediateRefreshPolicyTests
{
    [Fact]
    public void ShouldRequest_ShouldReturnFalse_WhenCrossPageInactive()
    {
        var shouldRequest = CrossPagePointerUpImmediateRefreshPolicy.ShouldRequest(
            crossPageDisplayActive: false,
            hadInkOperation: true,
            deferredRefreshRequested: true,
            updatePending: false);

        shouldRequest.Should().BeFalse();
    }

    [Fact]
    public void ShouldRequest_ShouldReturnFalse_WhenUpdatePending()
    {
        var shouldRequest = CrossPagePointerUpImmediateRefreshPolicy.ShouldRequest(
            crossPageDisplayActive: true,
            hadInkOperation: true,
            deferredRefreshRequested: false,
            updatePending: true);

        shouldRequest.Should().BeFalse();
    }

    [Fact]
    public void ShouldRequest_ShouldReturnTrue_WhenCrossPageActiveAndInkOrDeferred()
    {
        var byInk = CrossPagePointerUpImmediateRefreshPolicy.ShouldRequest(
            crossPageDisplayActive: true,
            hadInkOperation: true,
            deferredRefreshRequested: false,
            updatePending: false);
        var byDeferred = CrossPagePointerUpImmediateRefreshPolicy.ShouldRequest(
            crossPageDisplayActive: true,
            hadInkOperation: false,
            deferredRefreshRequested: true,
            updatePending: false);

        byInk.Should().BeTrue();
        byDeferred.Should().BeTrue();
    }
}
