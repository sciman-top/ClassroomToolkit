using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class PhotoOverlayEntryPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnEntryPlan_WhenPathExists()
    {
        var plan = PhotoOverlayEntryPolicy.Resolve(hasPath: true);

        plan.UpdateSequence.Should().BeTrue();
        plan.UpdateInkVisibility.Should().BeTrue();
        plan.SuppressNextOverlayActivatedApply.Should().BeTrue();
        plan.EnterPhotoMode.Should().BeTrue();
        plan.TouchPhotoSurface.Should().BeTrue();
        plan.FocusOverlay.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldReturnSequenceOnly_WhenPathMissing()
    {
        var plan = PhotoOverlayEntryPolicy.Resolve(hasPath: false);

        plan.UpdateSequence.Should().BeTrue();
        plan.UpdateInkVisibility.Should().BeFalse();
        plan.EnterPhotoMode.Should().BeFalse();
        plan.TouchPhotoSurface.Should().BeFalse();
        plan.FocusOverlay.Should().BeFalse();
    }
}
