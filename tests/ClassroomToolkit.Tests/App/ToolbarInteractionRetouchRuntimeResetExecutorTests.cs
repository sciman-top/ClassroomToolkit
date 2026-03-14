using System;
using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ToolbarInteractionRetouchRuntimeResetExecutorTests
{
    [Fact]
    public void Apply_ShouldClearQueueFlag_AndResetRetouchState()
    {
        var queued = true;
        var rerunRequested = true;
        var state = new ToolbarInteractionRetouchRuntimeState(
            LastRetouchUtc: DateTime.UtcNow,
            LastPreviewMouseDownUtc: DateTime.UtcNow);

        ToolbarInteractionRetouchRuntimeResetExecutor.Apply(
            ref queued,
            ref rerunRequested,
            ref state);

        queued.Should().BeFalse();
        rerunRequested.Should().BeFalse();
        state.Should().Be(ToolbarInteractionRetouchRuntimeState.Default);
    }
}
