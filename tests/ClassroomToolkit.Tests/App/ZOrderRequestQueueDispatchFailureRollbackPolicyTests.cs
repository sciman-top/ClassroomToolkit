using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ZOrderRequestQueueDispatchFailureRollbackPolicyTests
{
    [Fact]
    public void ShouldRollback_ShouldReturnTrue_WhenQueueDispatchFailed()
    {
        ZOrderRequestQueueDispatchFailureRollbackPolicy.ShouldRollback(
            FloatingDispatchQueueReason.QueueDispatchFailed).Should().BeTrue();
    }

    [Fact]
    public void ShouldRollback_ShouldReturnFalse_ForOtherReasons()
    {
        ZOrderRequestQueueDispatchFailureRollbackPolicy.ShouldRollback(
            FloatingDispatchQueueReason.MergedIntoQueuedRequest).Should().BeFalse();
        ZOrderRequestQueueDispatchFailureRollbackPolicy.ShouldRollback(
            FloatingDispatchQueueReason.QueuedNewRequest).Should().BeFalse();
        ZOrderRequestQueueDispatchFailureRollbackPolicy.ShouldRollback(
            FloatingDispatchQueueReason.None).Should().BeFalse();
    }
}
