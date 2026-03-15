using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageInteractiveInkSlotRemapPolicyTests
{
    [Fact]
    public void Resolve_ShouldClearCurrentFrame_WhenSlotChangedDuringInkOperationEvenIfPreservedExists()
    {
        var action = CrossPageInteractiveInkSlotRemapPolicy.Resolve(
            slotPageChanged: true,
            hasResolvedInkBitmap: false,
            hasCurrentInkFrame: true,
            hasPreservedInkFrame: true,
            inkOperationActive: true);

        action.Should().Be(CrossPageInteractiveInkSlotRemapAction.ClearCurrentFrame);
    }

    [Fact]
    public void Resolve_ShouldClearCurrentFrame_WhenSlotChangedDuringInkOperationAndNoPreservedFrame()
    {
        var action = CrossPageInteractiveInkSlotRemapPolicy.Resolve(
            slotPageChanged: true,
            hasResolvedInkBitmap: false,
            hasCurrentInkFrame: true,
            hasPreservedInkFrame: false,
            inkOperationActive: true);

        action.Should().Be(CrossPageInteractiveInkSlotRemapAction.ClearCurrentFrame);
    }

    [Fact]
    public void Resolve_ShouldClearCurrentFrame_WhenSlotChangedWithoutInkOperationAndNoPreservedFrame()
    {
        var action = CrossPageInteractiveInkSlotRemapPolicy.Resolve(
            slotPageChanged: true,
            hasResolvedInkBitmap: false,
            hasCurrentInkFrame: true,
            hasPreservedInkFrame: false,
            inkOperationActive: false);

        action.Should().Be(CrossPageInteractiveInkSlotRemapAction.ClearCurrentFrame);
    }

    [Fact]
    public void Resolve_ShouldUsePreservedFrame_WhenSlotChangedWithoutInkOperationAndPreservedExists()
    {
        var action = CrossPageInteractiveInkSlotRemapPolicy.Resolve(
            slotPageChanged: true,
            hasResolvedInkBitmap: false,
            hasCurrentInkFrame: true,
            hasPreservedInkFrame: true,
            inkOperationActive: false);

        action.Should().Be(CrossPageInteractiveInkSlotRemapAction.UsePreservedFrame);
    }

    [Fact]
    public void Resolve_ShouldKeepCurrentFrame_WhenSlotNotChanged()
    {
        var action = CrossPageInteractiveInkSlotRemapPolicy.Resolve(
            slotPageChanged: false,
            hasResolvedInkBitmap: false,
            hasCurrentInkFrame: true,
            hasPreservedInkFrame: true,
            inkOperationActive: true);

        action.Should().Be(CrossPageInteractiveInkSlotRemapAction.KeepCurrentFrame);
    }

    [Fact]
    public void Resolve_ShouldKeepCurrentFrame_WhenResolvedInkBitmapExists()
    {
        var action = CrossPageInteractiveInkSlotRemapPolicy.Resolve(
            slotPageChanged: true,
            hasResolvedInkBitmap: true,
            hasCurrentInkFrame: true,
            hasPreservedInkFrame: false,
            inkOperationActive: true);

        action.Should().Be(CrossPageInteractiveInkSlotRemapAction.KeepCurrentFrame);
    }
}
