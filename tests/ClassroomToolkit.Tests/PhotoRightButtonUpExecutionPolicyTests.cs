using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class PhotoRightButtonUpExecutionPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnPassThrough_WhenContextMenuShouldNotShow()
    {
        var plan = PhotoRightButtonUpExecutionPolicy.Resolve(shouldShowContextMenuOnUp: false);

        plan.Should().Be(new PhotoRightButtonUpExecutionPlan(
            Action: PhotoRightButtonUpAction.PassThrough,
            ShouldMarkHandled: false,
            ShouldClearPending: false));
    }

    [Fact]
    public void Resolve_ShouldReturnShowContextMenuPlan_WhenContextMenuShouldShow()
    {
        var plan = PhotoRightButtonUpExecutionPolicy.Resolve(shouldShowContextMenuOnUp: true);

        plan.Should().Be(new PhotoRightButtonUpExecutionPlan(
            Action: PhotoRightButtonUpAction.ShowContextMenu,
            ShouldMarkHandled: true,
            ShouldClearPending: true));
    }
}
