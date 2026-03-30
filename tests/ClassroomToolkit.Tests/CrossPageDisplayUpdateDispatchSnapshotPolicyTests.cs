using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageDisplayUpdateDispatchSnapshotPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnSnapshotWithAllFlags()
    {
        var snapshot = CrossPageDisplayUpdateDispatchSnapshotPolicy.Resolve(
            pending: true,
            panning: false,
            dragging: true,
            inkOperationActive: true);

        snapshot.Pending.Should().BeTrue();
        snapshot.Panning.Should().BeFalse();
        snapshot.Dragging.Should().BeTrue();
        snapshot.InkOperationActive.Should().BeTrue();
    }

    [Fact]
    public void FormatDiagnosticsTag_ShouldMatchExpectedShape()
    {
        var snapshot = new CrossPageDisplayUpdateDispatchSnapshot(
            Pending: true,
            Panning: false,
            Dragging: true,
            InkOperationActive: false);

        var tag = CrossPageDisplayUpdateDispatchSnapshot.FormatDiagnosticsTag(snapshot);

        tag.Should().Be("pending=True panning=False dragging=True");
    }
}
