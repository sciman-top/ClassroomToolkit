using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ToolbarInteractionDirectRepairRerunStateUpdaterTests
{
    [Fact]
    public void Request_ShouldSetRequestedFlag()
    {
        var requested = false;

        ToolbarInteractionDirectRepairRerunStateUpdater.Request(ref requested);

        requested.Should().BeTrue();
    }

    [Fact]
    public void TryConsume_ShouldReturnTrue_AndClearFlag()
    {
        var requested = true;

        var consumed = ToolbarInteractionDirectRepairRerunStateUpdater.TryConsume(ref requested);

        consumed.Should().BeTrue();
        requested.Should().BeFalse();
    }

    [Fact]
    public void Clear_ShouldResetFlag()
    {
        var requested = true;

        ToolbarInteractionDirectRepairRerunStateUpdater.Clear(ref requested);

        requested.Should().BeFalse();
    }
}
