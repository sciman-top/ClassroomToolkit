using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class PhotoPanEndRedrawPolicyTests
{
    [Fact]
    public void ShouldRequestInkRedraw_ShouldReturnFalse_WhenNoMovementAndNoCrossPageCommit()
    {
        PhotoPanEndRedrawPolicy.ShouldRequestInkRedraw(
            hadEffectiveMovement: false,
            hadCrossPageDragCommit: false).Should().BeFalse();
    }

    [Fact]
    public void ShouldRequestInkRedraw_ShouldReturnTrue_WhenEffectiveMovementOccurred()
    {
        PhotoPanEndRedrawPolicy.ShouldRequestInkRedraw(
            hadEffectiveMovement: true,
            hadCrossPageDragCommit: false).Should().BeTrue();
    }

    [Fact]
    public void ShouldRequestInkRedraw_ShouldReturnTrue_WhenCrossPageCommitOccurred()
    {
        PhotoPanEndRedrawPolicy.ShouldRequestInkRedraw(
            hadEffectiveMovement: false,
            hadCrossPageDragCommit: true).Should().BeTrue();
    }
}
