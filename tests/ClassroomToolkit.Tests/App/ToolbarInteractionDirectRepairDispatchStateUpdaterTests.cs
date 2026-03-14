using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ToolbarInteractionDirectRepairDispatchStateUpdaterTests
{
    [Fact]
    public void TryMarkQueued_ShouldSetTrue_OnFirstAttempt()
    {
        var queued = false;

        var marked = ToolbarInteractionDirectRepairDispatchStateUpdater.TryMarkQueued(ref queued);

        marked.Should().BeTrue();
        queued.Should().BeTrue();
    }

    [Fact]
    public void TryMarkQueued_ShouldReturnFalse_WhenAlreadyQueued()
    {
        var queued = true;

        var marked = ToolbarInteractionDirectRepairDispatchStateUpdater.TryMarkQueued(ref queued);

        marked.Should().BeFalse();
        queued.Should().BeTrue();
    }

    [Fact]
    public void Clear_ShouldResetFlag()
    {
        var queued = true;

        ToolbarInteractionDirectRepairDispatchStateUpdater.Clear(ref queued);

        queued.Should().BeFalse();
    }
}
