using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPagePointerUpRefreshPolicyTests
{
    [Fact]
    public void ShouldSchedulePostInputRefresh_ShouldReturnFalse_WhenCrossPageDisplayInactive()
    {
        CrossPagePointerUpRefreshPolicy.ShouldSchedulePostInputRefresh(
            crossPageDisplayActive: false,
            hadInkOperation: true,
            deferredRefreshRequested: true).Should().BeFalse();
    }

    [Fact]
    public void ShouldSchedulePostInputRefresh_ShouldReturnTrue_WhenInkOperationEndedInCrossPage()
    {
        CrossPagePointerUpRefreshPolicy.ShouldSchedulePostInputRefresh(
            crossPageDisplayActive: true,
            hadInkOperation: true,
            deferredRefreshRequested: false).Should().BeTrue();
    }

    [Fact]
    public void ShouldSchedulePostInputRefresh_ShouldReturnTrue_WhenDeferredRefreshRequestedInCrossPage()
    {
        CrossPagePointerUpRefreshPolicy.ShouldSchedulePostInputRefresh(
            crossPageDisplayActive: true,
            hadInkOperation: false,
            deferredRefreshRequested: true).Should().BeTrue();
    }

    [Fact]
    public void ShouldSchedulePostInputRefresh_ShouldReturnFalse_WhenNoInkAndNoDeferred()
    {
        CrossPagePointerUpRefreshPolicy.ShouldSchedulePostInputRefresh(
            crossPageDisplayActive: true,
            hadInkOperation: false,
            deferredRefreshRequested: false).Should().BeFalse();
    }
}
