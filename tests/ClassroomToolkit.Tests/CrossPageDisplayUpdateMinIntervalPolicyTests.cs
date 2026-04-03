using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageDisplayUpdateMinIntervalPolicyTests
{
    [Fact]
    public void ResolveMs_ShouldUsePanInterval_WhenPanningActive()
    {
        var value = CrossPageDisplayUpdateMinIntervalPolicy.ResolveMs(
            photoPanning: true,
            crossPageDragging: false,
            inkOperationActive: false,
            draggingMinIntervalMs: 24,
            normalMinIntervalMs: 16);

        value.Should().Be(24);
    }

    [Fact]
    public void ResolveMs_ShouldUseInkInterval_WhenOnlyInkActive()
    {
        var value = CrossPageDisplayUpdateMinIntervalPolicy.ResolveMs(
            photoPanning: false,
            crossPageDragging: false,
            inkOperationActive: true,
            draggingMinIntervalMs: 24,
            normalMinIntervalMs: 16);

        value.Should().Be(24);
    }

    [Fact]
    public void ResolveMs_ShouldUseWiderInterval_WhenPanAndInkActive()
    {
        var value = CrossPageDisplayUpdateMinIntervalPolicy.ResolveMs(
            photoPanning: true,
            crossPageDragging: false,
            inkOperationActive: true,
            draggingMinIntervalMs: 24,
            normalMinIntervalMs: 16);

        value.Should().Be(24);
    }

    [Fact]
    public void ResolveMs_ShouldUseNormalInterval_WhenNoInteraction()
    {
        var value = CrossPageDisplayUpdateMinIntervalPolicy.ResolveMs(
            photoPanning: false,
            crossPageDragging: false,
            inkOperationActive: false,
            draggingMinIntervalMs: 24,
            normalMinIntervalMs: 16);

        value.Should().Be(16);
    }
}
