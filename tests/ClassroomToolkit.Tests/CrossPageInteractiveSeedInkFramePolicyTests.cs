using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageInteractiveSeedInkFramePolicyTests
{
    [Fact]
    public void ShouldReplaceFrame_ShouldReturnTrue_WhenInkDisplayDisabled()
    {
        CrossPageInteractiveSeedInkFramePolicy
            .ShouldReplaceFrame(
                inkShowEnabled: false,
                hasCurrentFrame: true,
                hasResolvedTargetFrame: false,
                slotPageChanged: false)
            .Should()
            .BeTrue();
    }

    [Fact]
    public void ShouldReplaceFrame_ShouldReturnTrue_WhenTargetFrameResolved()
    {
        CrossPageInteractiveSeedInkFramePolicy
            .ShouldReplaceFrame(
                inkShowEnabled: true,
                hasCurrentFrame: true,
                hasResolvedTargetFrame: true,
                slotPageChanged: false)
            .Should()
            .BeTrue();
    }

    [Fact]
    public void ShouldReplaceFrame_ShouldKeepCurrentFrame_WhenTargetMissingAndCurrentExists()
    {
        CrossPageInteractiveSeedInkFramePolicy
            .ShouldReplaceFrame(
                inkShowEnabled: true,
                hasCurrentFrame: true,
                hasResolvedTargetFrame: false,
                slotPageChanged: false)
            .Should()
            .BeFalse();
    }

    [Fact]
    public void ShouldReplaceFrame_ShouldAllowNullAssignment_WhenNoCurrentFrame()
    {
        CrossPageInteractiveSeedInkFramePolicy
            .ShouldReplaceFrame(
                inkShowEnabled: true,
                hasCurrentFrame: false,
                hasResolvedTargetFrame: false,
                slotPageChanged: false)
            .Should()
            .BeTrue();
    }

    [Fact]
    public void ShouldReplaceFrame_ShouldClearStaleFrame_WhenSlotRemappedAndTargetMissing()
    {
        CrossPageInteractiveSeedInkFramePolicy
            .ShouldReplaceFrame(
                inkShowEnabled: true,
                hasCurrentFrame: true,
                hasResolvedTargetFrame: false,
                slotPageChanged: true)
            .Should()
            .BeTrue();
    }
}
