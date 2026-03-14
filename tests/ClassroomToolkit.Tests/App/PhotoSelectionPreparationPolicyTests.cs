using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class PhotoSelectionPreparationPolicyTests
{
    [Fact]
    public void Resolve_ShouldCloseImageManager_AndDisableWhiteboard_WhenNeeded()
    {
        var plan = PhotoSelectionPreparationPolicy.Resolve(
            imageManagerVisible: true,
            whiteboardActive: true);

        plan.CloseImageManager.Should().BeTrue();
        plan.DisableWhiteboard.Should().BeTrue();
        plan.SuppressPresentationForeground.Should().BeTrue();
        plan.PresentationForegroundSuppressionMs.Should().Be(PhotoSelectionPreparationDefaults.PresentationForegroundSuppressionMs);
    }

    [Fact]
    public void Resolve_ShouldStillSuppressForeground_WhenNoOtherCleanupNeeded()
    {
        var plan = PhotoSelectionPreparationPolicy.Resolve(
            imageManagerVisible: false,
            whiteboardActive: false);

        plan.CloseImageManager.Should().BeFalse();
        plan.DisableWhiteboard.Should().BeFalse();
        plan.SuppressPresentationForeground.Should().BeTrue();
    }
}
