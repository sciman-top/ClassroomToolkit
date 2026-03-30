using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ToolbarInteractionRetouchStateUpdaterTests
{
    [Fact]
    public void MarkPreviewMouseDown_ShouldUpdateLastPreviewMouseDownUtc()
    {
        var state = ToolbarInteractionRetouchRuntimeState.Default;
        var nowUtc = new DateTime(2026, 3, 8, 8, 0, 0, DateTimeKind.Utc);

        ToolbarInteractionRetouchStateUpdater.MarkPreviewMouseDown(ref state, nowUtc);

        state.LastPreviewMouseDownUtc.Should().Be(nowUtc);
    }

    [Fact]
    public void MarkRetouched_ShouldUpdateLastRetouchUtc()
    {
        var state = ToolbarInteractionRetouchRuntimeState.Default;
        var nowUtc = new DateTime(2026, 3, 7, 14, 0, 0, DateTimeKind.Utc);

        ToolbarInteractionRetouchStateUpdater.MarkRetouched(ref state, nowUtc);

        state.LastRetouchUtc.Should().Be(nowUtc);
    }

    [Fact]
    public void MarkRetouched_ShouldKeepLastPreviewMouseDownUtc()
    {
        var previewUtc = new DateTime(2026, 3, 8, 8, 0, 0, DateTimeKind.Utc);
        var state = ToolbarInteractionRetouchRuntimeState.Default with
        {
            LastPreviewMouseDownUtc = previewUtc
        };
        var nowUtc = new DateTime(2026, 3, 8, 8, 0, 1, DateTimeKind.Utc);

        ToolbarInteractionRetouchStateUpdater.MarkRetouched(ref state, nowUtc);

        state.LastRetouchUtc.Should().Be(nowUtc);
        state.LastPreviewMouseDownUtc.Should().Be(previewUtc);
    }

    [Fact]
    public void Reset_ShouldRestoreDefaultState()
    {
        var state = new ToolbarInteractionRetouchRuntimeState(
            LastRetouchUtc: new DateTime(2026, 3, 8, 9, 0, 0, DateTimeKind.Utc),
            LastPreviewMouseDownUtc: new DateTime(2026, 3, 8, 8, 0, 0, DateTimeKind.Utc));

        ToolbarInteractionRetouchStateUpdater.Reset(ref state);

        state.Should().Be(ToolbarInteractionRetouchRuntimeState.Default);
    }
}
