using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class InkSidecarLoadAdmissionPolicyTests
{
    [Fact]
    public void ShouldApplyLoadedSnapshot_ShouldReturnTrue_WhenRuntimeStateUnknown()
    {
        var shouldApply = InkSidecarLoadAdmissionPolicy.ShouldApplyLoadedSnapshot(
            runtimeStateKnown: false,
            runtimeHash: string.Empty,
            runtimeDirty: false,
            loadedHash: "A");

        shouldApply.Should().BeTrue();
    }

    [Fact]
    public void ShouldApplyLoadedSnapshot_ShouldReturnTrue_WhenHashMatches()
    {
        var shouldApply = InkSidecarLoadAdmissionPolicy.ShouldApplyLoadedSnapshot(
            runtimeStateKnown: true,
            runtimeHash: "A",
            runtimeDirty: true,
            loadedHash: "A");

        shouldApply.Should().BeTrue();
    }

    [Fact]
    public void ShouldApplyLoadedSnapshot_ShouldReturnFalse_WhenRuntimeIsDirtyAndHashMismatches()
    {
        var shouldApply = InkSidecarLoadAdmissionPolicy.ShouldApplyLoadedSnapshot(
            runtimeStateKnown: true,
            runtimeHash: "A",
            runtimeDirty: true,
            loadedHash: "B");

        shouldApply.Should().BeFalse();
    }

    [Fact]
    public void ShouldApplyLoadedSnapshot_ShouldReturnFalse_WhenRuntimeIsClearedButLoadedIsNotEmpty()
    {
        var shouldApply = InkSidecarLoadAdmissionPolicy.ShouldApplyLoadedSnapshot(
            runtimeStateKnown: true,
            runtimeHash: "empty",
            runtimeDirty: false,
            loadedHash: "B");

        shouldApply.Should().BeFalse();
    }

    [Fact]
    public void ShouldApplyLoadedSnapshot_ShouldReturnTrue_WhenRuntimeIsCleanAndNonEmptyDespiteMismatch()
    {
        var shouldApply = InkSidecarLoadAdmissionPolicy.ShouldApplyLoadedSnapshot(
            runtimeStateKnown: true,
            runtimeHash: "A",
            runtimeDirty: false,
            loadedHash: "B");

        shouldApply.Should().BeTrue();
    }
}
