using FluentAssertions;
using ClassroomToolkit.App.Paint;

namespace ClassroomToolkit.Tests;

public sealed class InkAutoSaveSnapshotAdmissionPolicyTests
{
    [Fact]
    public void ShouldPersistSnapshot_ShouldReturnTrue_WhenRuntimeStateIsUnknown()
    {
        var shouldPersist = InkAutoSaveSnapshotAdmissionPolicy.ShouldPersistSnapshot(
            runtimeStateKnown: false,
            runtimeHash: string.Empty,
            snapshotHash: "strokes-v1");

        shouldPersist.Should().BeTrue();
    }

    [Fact]
    public void ShouldPersistSnapshot_ShouldReturnTrue_WhenRuntimeHashMatchesSnapshot()
    {
        var shouldPersist = InkAutoSaveSnapshotAdmissionPolicy.ShouldPersistSnapshot(
            runtimeStateKnown: true,
            runtimeHash: "empty",
            snapshotHash: "empty");

        shouldPersist.Should().BeTrue();
    }

    [Fact]
    public void ShouldPersistSnapshot_ShouldReturnFalse_WhenRuntimeHashAdvanced()
    {
        var shouldPersist = InkAutoSaveSnapshotAdmissionPolicy.ShouldPersistSnapshot(
            runtimeStateKnown: true,
            runtimeHash: "empty",
            snapshotHash: "strokes-v1");

        shouldPersist.Should().BeFalse();
    }
}
