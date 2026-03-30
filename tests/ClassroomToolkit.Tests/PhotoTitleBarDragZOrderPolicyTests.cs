using System.Windows.Input;
using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class PhotoTitleBarDragZOrderPolicyTests
{
    [Fact]
    public void Resolve_ShouldAllowDragAndRequestSingleNonForcedRetouch_WhenLeftButtonDragInWindowedPhotoMode()
    {
        var plan = PhotoTitleBarDragZOrderPolicy.Resolve(
            photoModeActive: true,
            photoFullscreen: false,
            changedButton: MouseButton.Left);

        plan.CanDrag.Should().BeTrue();
        plan.RequestZOrderBeforeDrag.Should().BeFalse();
        plan.RequestZOrderAfterDrag.Should().BeTrue();
        plan.ForceAfterDrag.Should().BeFalse();
    }

    [Theory]
    [InlineData(false, false, MouseButton.Left)]
    [InlineData(true, true, MouseButton.Left)]
    [InlineData(true, false, MouseButton.Right)]
    public void Resolve_ShouldBlockDrag_WhenPreconditionsNotMet(
        bool photoModeActive,
        bool photoFullscreen,
        MouseButton changedButton)
    {
        var plan = PhotoTitleBarDragZOrderPolicy.Resolve(
            photoModeActive,
            photoFullscreen,
            changedButton);

        plan.CanDrag.Should().BeFalse();
        plan.RequestZOrderBeforeDrag.Should().BeFalse();
        plan.RequestZOrderAfterDrag.Should().BeFalse();
        plan.ForceAfterDrag.Should().BeFalse();
    }
}

